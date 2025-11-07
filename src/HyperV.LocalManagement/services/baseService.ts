const BASE_URL = '/api/v1';

export class ApiError extends Error {
    public readonly statusCode: number;
    public readonly details: any;

    constructor(message: string, statusCode: number, details?: any) {
        super(message);
        this.name = 'ApiError';
        this.statusCode = statusCode;
        this.details = details;
    }
}

export async function fetchApi(url: string, options: RequestInit = {}) {
    try {
        const response = await fetch(`${BASE_URL}${url}`, {
            ...options,
            headers: {
                'Content-Type': 'application/json',
                ...options.headers,
            },
        });

        if (!response.ok) {
            let errorData;
            try {
                errorData = await response.json();
            } catch (e) {
                throw new ApiError(`HTTP error! status: ${response.status} ${response.statusText}`, response.status);
            }
            const errorMessage = errorData.error || `Request failed with status: ${response.status}`;
            throw new ApiError(errorMessage, response.status, errorData.details);
        }

        if (response.status === 204 || response.headers.get('content-length') === '0') {
            return;
        }

        return response.json();
    } catch (error) {
        if (error instanceof TypeError) {
            throw new ApiError('Network Error: Could not connect to the API. Please ensure the Hyper-V Agent is running.', 0);
        }
        throw error;
    }
}