import { HttpErrorResponse } from '@angular/common/http';

/** RFC 7807 problem returned by the API (title holds the human message; errors holds per-field messages). */
export interface ApiProblem {
  title?: string;
  status?: number;
  code?: string;
  errors?: Record<string, string[]>;
}

/** Every failed API call surfaces as an ApiError with a message that can be shown to the user as is. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly code?: string,
    /** Field name → messages, for 400 validation problems. Keys are camelCase, matching form field names. */
    readonly fields: Record<string, string[]> = {},
  ) {
    super(message);
  }

  /** First message for a form field, if the server rejected it. */
  field(name: string): string | null {
    return this.fields[name]?.[0] ?? null;
  }

  static from(e: unknown): ApiError {
    if (e instanceof ApiError) return e;
    if (e instanceof HttpErrorResponse) {
      const problem = (e.error ?? {}) as ApiProblem;
      if (e.status === 0) return new ApiError("Can't reach the server. Check your connection and try again.", 0);
      if (e.status === 401) return new ApiError(problem.title ?? 'Your session has ended. Sign in again.', 401, problem.code);
      if (e.status === 403) return new ApiError(problem.title ?? "Your role doesn't allow this action.", 403, problem.code);
      if (e.status === 429) return new ApiError('Too many requests. Wait a minute and try again.', 429);
      const fields = problem.errors ?? {};
      const first = Object.values(fields)[0]?.[0];
      return new ApiError(first ?? problem.title ?? `Request failed (${e.status}).`, e.status, problem.code, fields);
    }
    return new ApiError(e instanceof Error ? e.message : 'Something went wrong.', -1);
  }
}

export const errorMessage = (e: unknown) => ApiError.from(e).message;
