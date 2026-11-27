import { HttpErrorResponse } from '@angular/common/http';
import { ApiError } from './api-error';

describe('ApiError', () => {
  const http = (status: number, error: unknown) => new HttpErrorResponse({ status, error });

  it('uses the problem title as the message', () => {
    const e = ApiError.from(http(409, { title: 'Table code already used.', code: 'Table.DuplicateCode' }));
    expect(e.message).toBe('Table code already used.');
    expect(e.code).toBe('Table.DuplicateCode');
    expect(e.status).toBe(409);
  });

  it('keeps validation errors per field and leads with the first one', () => {
    const e = ApiError.from(http(400, { title: 'One or more validation errors occurred.', errors: { price: ['Price must be 0 or more.'], name: ['Name is required.'] } }));
    expect(e.field('name')).toBe('Name is required.');
    expect(e.field('missing')).toBeNull();
    expect(e.message).toBe('Price must be 0 or more.');
  });

  it('explains network failures in plain words', () => {
    expect(ApiError.from(http(0, null)).message).toMatch(/Can't reach the server/);
  });
});
