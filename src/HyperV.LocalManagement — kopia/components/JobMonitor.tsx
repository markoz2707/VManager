import React, { useState, useEffect } from 'react';
import { getStorageJobs } from '../services/jobService'; // Import job service
import { StorageJob } from '../types'; // Assume StorageJob type from types

const POLL_INTERVAL = 30000; // 30 seconds

const JobMonitor: React.FC = () => {
  const [jobs, setJobs] = useState<StorageJob[]>([]);
  const [isExpanded, setIsExpanded] = useState(false);
  const [loading, setLoading] = useState(false);

  const fetchJobs = async () => {
    setLoading(true);
    try {
      // Fetch storage jobs (active and recent completed within 5 min)
      const data: StorageJob[] = await getStorageJobs();
      const now = new Date().getTime();
      const recentCutoff = now - 5 * 60 * 1000; // 5 minutes in ms

      const activeJobs = data.filter(job => 
        job.status === 'Running' || job.status === 'Pending' || job.status !== 'Completed'
      );

      const recentCompleted = data
        .filter(job => job.status === 'Completed' && new Date(job.completionTime || 0).getTime() > recentCutoff)
        .sort((a, b) => new Date(b.completionTime || 0).getTime() - new Date(a.completionTime || 0).getTime());

      setJobs([...activeJobs, ...recentCompleted]);
    } catch (error) {
      console.error('Failed to fetch jobs:', error);
      setJobs([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchJobs(); // Initial load

    const interval = setInterval(() => {
      fetchJobs(); // Poll every 30s
    }, POLL_INTERVAL);

    return () => clearInterval(interval);
  }, []);

  const toggleExpanded = () => setIsExpanded(!isExpanded);

  if (loading && jobs.length === 0) return <div>Loading jobs...</div>;

  return (
    <div className="job-monitor" style={{ width: '100%' }}>
      <div className="job-monitor-header">
        <h3>Job Monitor</h3>
        <button onClick={toggleExpanded} className="toggle-button">
          {isExpanded ? 'Hide' : 'Show'} Jobs ({jobs.length})
        </button>
      </div>
      
      {isExpanded && (
        <div className="job-list" style={{ maxHeight: '400px', overflowY: 'auto', padding: '10px' }}>
          {jobs.length === 0 ? (
            <p>No active or recent jobs.</p>
          ) : (
            <ul style={{ listStyleType: 'none', padding: 0 }}>
              {jobs.map(job => (
                <li key={job.id} style={{ padding: '5px 0', borderBottom: '1px solid #eee' }}>
                  <strong>{job.operation || 'Job'}</strong> - Status: {job.status}
                  {job.completionTime && <span> (Completed: {new Date(job.completionTime).toLocaleString()})</span>}
                  {job.errors && job.errors.length > 0 && (
                    <details style={{ marginLeft: '20px', fontSize: '0.9em', color: '#d9534f' }}>
                      <summary>WMI Errors</summary>
                      <ul style={{ margin: 0, paddingLeft: '20px' }}>
                        {job.errors.map((err, idx) => <li key={idx}>{err}</li>)}
                      </ul>
                    </details>
                  )}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
      
      <style>
        {`
          .job-monitor {
            width: 100%;
            border: 1px solid #ccc;
            margin-bottom: 10px;
          }
          
          .job-monitor-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 10px;
            background-color: #f5f5f5;
          }
          
          .toggle-button {
            background-color: #007bff;
            color: white;
            border: none;
            padding: 5px 10px;
            cursor: pointer;
            border-radius: 4px;
          }
          
          .job-list li {
            padding: 5px 0;
          }
          
          details {
            margin-left: 20px;
            font-size: 0.9em;
            color: #d9534f;
          }
        `}
      </style>
    </div>
  );
};

export default JobMonitor;