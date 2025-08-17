-- AppTrace PostgreSQL Database Schema
-- Optimized for high-volume telemetry data with JSONB attributes

-- Create database (run this separately if needed)
-- CREATE DATABASE apptrace;

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ==========================================
-- LOGS TABLE
-- ==========================================
CREATE TABLE IF NOT EXISTS logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    timestamp TIMESTAMPTZ NOT NULL,
    trace_id TEXT,
    span_id TEXT,
    severity TEXT,
    body TEXT,
    attributes JSONB,
    service_name TEXT NOT NULL DEFAULT 'unknown',
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Indexes for logs table
CREATE INDEX IF NOT EXISTS idx_logs_timestamp ON logs(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_logs_service_name ON logs(service_name);
CREATE INDEX IF NOT EXISTS idx_logs_trace_id ON logs(trace_id) WHERE trace_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_logs_severity ON logs(severity);
CREATE INDEX IF NOT EXISTS idx_logs_attributes_gin ON logs USING GIN (attributes);
CREATE INDEX IF NOT EXISTS idx_logs_body_text ON logs USING GIN (to_tsvector('english', body));

-- ==========================================
-- TRACES TABLE  
-- ==========================================
CREATE TABLE IF NOT EXISTS traces (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    trace_id TEXT NOT NULL,
    span_id TEXT NOT NULL,
    parent_span_id TEXT,
    name TEXT NOT NULL,
    start_time TIMESTAMPTZ NOT NULL,
    end_time TIMESTAMPTZ NOT NULL,
    duration_ms DOUBLE PRECISION GENERATED ALWAYS AS (EXTRACT(EPOCH FROM (end_time - start_time)) * 1000) STORED,
    attributes JSONB,
    status TEXT DEFAULT 'OK',
    service_name TEXT NOT NULL DEFAULT 'unknown',
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Indexes for traces table
CREATE INDEX IF NOT EXISTS idx_traces_trace_id ON traces(trace_id);
CREATE INDEX IF NOT EXISTS idx_traces_span_id ON traces(span_id);
CREATE INDEX IF NOT EXISTS idx_traces_parent_span_id ON traces(parent_span_id) WHERE parent_span_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_traces_start_time ON traces(start_time DESC);
CREATE INDEX IF NOT EXISTS idx_traces_service_name ON traces(service_name);
CREATE INDEX IF NOT EXISTS idx_traces_duration ON traces(duration_ms DESC);
CREATE INDEX IF NOT EXISTS idx_traces_status ON traces(status);
CREATE INDEX IF NOT EXISTS idx_traces_attributes_gin ON traces USING GIN (attributes);

-- ==========================================
-- METRICS TABLE
-- ==========================================
CREATE TABLE IF NOT EXISTS metrics (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name TEXT NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL,
    value DOUBLE PRECISION NOT NULL,
    attributes JSONB,
    service_name TEXT NOT NULL DEFAULT 'unknown',
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Indexes for metrics table
CREATE INDEX IF NOT EXISTS idx_metrics_name ON metrics(name);
CREATE INDEX IF NOT EXISTS idx_metrics_timestamp ON metrics(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_metrics_service_name ON metrics(service_name);
CREATE INDEX IF NOT EXISTS idx_metrics_name_timestamp ON metrics(name, timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_metrics_attributes_gin ON metrics USING GIN (attributes);

-- ==========================================
-- PARTITIONING FOR SCALE (Optional)
-- ==========================================
-- Uncomment these for time-based partitioning if you expect very high volume

-- -- Partition logs by month
-- SELECT create_time_partition('logs', 'timestamp', '1 month', p_start_date => NOW() - INTERVAL '1 month');

-- -- Partition traces by month  
-- SELECT create_time_partition('traces', 'start_time', '1 month', p_start_date => NOW() - INTERVAL '1 month');

-- -- Partition metrics by week (higher volume expected)
-- SELECT create_time_partition('metrics', 'timestamp', '1 week', p_start_date => NOW() - INTERVAL '1 week');

-- ==========================================
-- PERFORMANCE OPTIMIZATION SETTINGS
-- ==========================================

-- Optimize for bulk inserts
ALTER TABLE logs SET (fillfactor = 90);
ALTER TABLE traces SET (fillfactor = 90);  
ALTER TABLE metrics SET (fillfactor = 85); -- Higher churn expected

-- Set statistics targets for better query planning
ALTER TABLE logs ALTER COLUMN service_name SET STATISTICS 1000;
ALTER TABLE logs ALTER COLUMN severity SET STATISTICS 1000;
ALTER TABLE traces ALTER COLUMN service_name SET STATISTICS 1000;
ALTER TABLE traces ALTER COLUMN trace_id SET STATISTICS 1000;
ALTER TABLE metrics ALTER COLUMN name SET STATISTICS 1000;
ALTER TABLE metrics ALTER COLUMN service_name SET STATISTICS 1000;

-- ==========================================
-- CLEANUP/RETENTION POLICIES (Optional)
-- ==========================================

-- Function to clean up old data (adjust retention as needed)
CREATE OR REPLACE FUNCTION cleanup_old_telemetry_data()
RETURNS void AS $$
BEGIN
    -- Delete logs older than 30 days
    DELETE FROM logs WHERE timestamp < NOW() - INTERVAL '30 days';
    
    -- Delete traces older than 30 days
    DELETE FROM traces WHERE start_time < NOW() - INTERVAL '30 days';
    
    -- Delete metrics older than 14 days (higher volume)
    DELETE FROM metrics WHERE timestamp < NOW() - INTERVAL '14 days';
    
    -- Log cleanup activity
    RAISE NOTICE 'Telemetry data cleanup completed at %', NOW();
END;
$$ LANGUAGE plpgsql;

-- Optionally schedule cleanup (requires pg_cron extension)
-- SELECT cron.schedule('cleanup-telemetry', '0 2 * * *', 'SELECT cleanup_old_telemetry_data();');

-- ==========================================
-- VIEWS FOR COMMON QUERIES
-- ==========================================

-- Recent errors view
CREATE OR REPLACE VIEW recent_errors AS
SELECT 
    l.timestamp,
    l.service_name,
    l.severity,
    l.body,
    l.trace_id,
    t.name as trace_name,
    t.duration_ms
FROM logs l
LEFT JOIN traces t ON l.trace_id = t.trace_id AND t.parent_span_id IS NULL
WHERE l.severity IN ('ERROR', 'FATAL', 'Error', 'Fatal')
    AND l.timestamp > NOW() - INTERVAL '24 hours'
ORDER BY l.timestamp DESC;

-- Service performance summary
CREATE OR REPLACE VIEW service_performance AS
SELECT 
    service_name,
    COUNT(*) as request_count,
    AVG(duration_ms) as avg_duration_ms,
    PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY duration_ms) as p50_duration_ms,
    PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY duration_ms) as p95_duration_ms,
    PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY duration_ms) as p99_duration_ms,
    COUNT(*) FILTER (WHERE status != 'OK') as error_count
FROM traces 
WHERE start_time > NOW() - INTERVAL '1 hour'
    AND parent_span_id IS NULL -- Only root spans
GROUP BY service_name
ORDER BY request_count DESC;

-- Recent metrics summary
CREATE OR REPLACE VIEW recent_metrics_summary AS
SELECT 
    name,
    service_name,
    COUNT(*) as data_points,
    AVG(value) as avg_value,
    MIN(value) as min_value,
    MAX(value) as max_value,
    STDDEV(value) as stddev_value
FROM metrics 
WHERE timestamp > NOW() - INTERVAL '1 hour'
GROUP BY name, service_name
ORDER BY name, service_name;

-- ==========================================
-- GRANTS (Adjust as needed)
-- ==========================================

-- Create app user (adjust username/password as needed)
-- CREATE USER apptrace_app WITH PASSWORD 'your_secure_password';
-- GRANT CONNECT ON DATABASE apptrace TO apptrace_app;
-- GRANT USAGE ON SCHEMA public TO apptrace_app;
-- GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO apptrace_app;
-- GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO apptrace_app;

COMMIT;