import React, { useState } from 'react';
import JobMonitor from './JobMonitor';

const Footer: React.FC = () => {
  const [showJobs, setShowJobs] = useState(false);

  return (
    <footer className="app-footer">
      <div className="footer-content">
        <button 
          onClick={() => setShowJobs(!showJobs)} 
          className="jobs-toggle"
          style={{ backgroundColor: '#6c757d', color: 'white', border: 'none', padding: '10px 20px', cursor: 'pointer', borderRadius: '4px', margin: '0 auto' }}
        >
          {showJobs ? 'Hide' : 'Show'} Active Jobs
        </button>
        {showJobs && <JobMonitor />}
      </div>
      
      <style>
        {`
          .app-footer {
            width: 100%;
            background-color: #f8f9fa;
            padding: 20px;
            border-top: 1px solid #dee2e6;
            margin-top: auto;
          }
          
          .footer-content {
            display: flex;
            justify-content: center;
          }
        `}
      </style>
    </footer>
  );
};

export default Footer;