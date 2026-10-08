/**
 * Central API error handling.
 *
 * Backends return errors in several different shapes. This normalizes them into
 * ONE clean, user-facing message so the whole app shows something meaningful
 * (e.g. "An account already exists for this Aadhar number") instead of a generic
 * "Something failed".
 *
 * Shapes handled:
 *   - { detail: { error_code, message } }   custom HTTPException (most endpoints)
 *   - { detail: "some string" }             plain HTTPException
 *   - { detail: [ { loc, msg, type } ] }    FastAPI 422 validation errors
 *   - { message: "..." }                    a few endpoints
 *   - no response at all                    network error / timeout / CORS
 */

// Service-layer messages are prefixed like "Validation error in aadhar_number: ..."
// — strip that technical prefix so the UI shows just the human part.
const FIELD_PREFIX = /^Validation error in [^:]+:\s*/i;

const clean = (msg) => String(msg).replace(FIELD_PREFIX, '').trim();

/**
 * Machine-readable error code from the backend (e.g. "VALIDATION_ERROR",
 * "INVALID_PIN"), or null. Useful when a component needs to branch on the code.
 */
export function getApiErrorCode(error) {
  const detail = error?.response?.data?.detail;
  if (detail && typeof detail === 'object' && !Array.isArray(detail)) {
    return detail.error_code || null;
  }
  return error?.response?.data?.error_code || null;
}

/**
 * A clean, user-facing message for any axios error.
 * @param {unknown} error   the caught axios error
 * @param {string} fallback shown only when the server gave no usable message
 */
export function getApiErrorMessage(error, fallback = 'Something went wrong. Please try again.') {
  // No HTTP response => network error, timeout, or CORS.
  if (!error?.response) {
    if (error?.code === 'ECONNABORTED') return 'The request timed out. Please try again.';
    if (/network/i.test(error?.message || '')) {
      return 'Cannot reach the server. Please check your connection and try again.';
    }
    return error?.message || fallback;
  }

  const { status, data } = error.response;
  const detail = data?.detail;

  // { detail: { error_code, message } }
  if (detail && typeof detail === 'object' && !Array.isArray(detail) && detail.message) {
    return clean(detail.message);
  }

  // { detail: [ { loc, msg, type } ] }  — FastAPI 422 validation
  if (Array.isArray(detail) && detail.length) {
    return detail
      .map((d) => {
        const field = Array.isArray(d.loc) ? d.loc[d.loc.length - 1] : null;
        return field && field !== 'body' ? `${field}: ${d.msg}` : d.msg;
      })
      .filter(Boolean)
      .join('; ');
  }

  // { detail: "string" }
  if (typeof detail === 'string' && detail.trim()) return clean(detail);

  // { message: "..." }
  if (typeof data?.message === 'string' && data.message.trim()) return clean(data.message);

  // No usable body — fall back on the HTTP status.
  switch (status) {
    case 401: return 'Your session has expired. Please log in again.';
    case 403: return 'You do not have permission to perform this action.';
    case 404: return 'The requested item was not found.';
    case 409: return 'This conflicts with existing data.';
    case 429: return 'Too many requests. Please slow down and try again.';
    default:  return status >= 500
      ? 'The server ran into a problem. Please try again later.'
      : fallback;
  }
}

export default getApiErrorMessage;
