// Fix: Changed protocol from http to https and hostname to localhost to match the API environment
const BASE_URL = 'https://localhost:8743/api/v1';

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

            let errorMessage = errorData.error || `Request failed with status: ${response.status}`;
            let errorDetails = errorData.details;

            if (errorData.errors && typeof errorData.errors === 'object') {
                const validationErrors = Object.entries(errorData.errors)
                    .map(([field, messages]) => {
                        const fieldName = field.replace('$.', '');
                        return `${fieldName}: ${(messages as string[]).join(', ')}`;
                    })
                    .join('; ');
                errorMessage = `${errorData.title || 'Validation Error'}. ${validationErrors}`;
                errorDetails = errorData.errors;
            } else if (errorData.title) {
                errorMessage = errorData.title;
            }

            throw new ApiError(errorMessage, response.status, errorDetails);
        }

        if (response.status === 204 || response.headers.get('content-length') === '0') {
            return;
        }

        return response.json();
    } catch (error) {
        if (error instanceof TypeError) {
            throw new ApiError('Network Error: Could not connect to the API. Please ensure the Hyper-V Agent is running and the SSL certificate is trusted.', 0);
        }
        throw error;
    }
}