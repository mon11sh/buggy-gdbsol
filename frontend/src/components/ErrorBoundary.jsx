import { Component } from 'react';

/**
 * Catches render-time errors in the page subtree so a single bad component
 * (or a bug a trainee is mid-way through fixing) shows a readable message
 * instead of white-screening the whole app.
 *
 * Reset via `resetKey`: when the key changes (e.g. the route path), the
 * boundary clears its error state and re-renders its children — so navigating
 * to another page recovers automatically.
 */
class ErrorBoundary extends Component {
  constructor(props) {
    super(props);
    this.state = { error: null };
  }

  static getDerivedStateFromError(error) {
    return { error };
  }

  componentDidUpdate(prevProps) {
    if (prevProps.resetKey !== this.props.resetKey && this.state.error) {
      this.setState({ error: null });
    }
  }

  render() {
    if (this.state.error) {
      return (
        <div className="p-8">
          <div className="max-w-2xl mx-auto bg-red-50 border border-red-200 rounded-xl p-6">
            <h2 className="text-lg font-semibold text-red-800">This page hit an error</h2>
            <p className="text-red-700 text-sm mt-2">
              The rest of the app is still working — use the menu to navigate away, or
              reload after fixing the underlying issue.
            </p>
            <pre className="mt-4 text-xs text-red-900 bg-red-100 rounded-lg p-3 overflow-x-auto whitespace-pre-wrap">
              {String(this.state.error?.message || this.state.error)}
            </pre>
          </div>
        </div>
      );
    }
    return this.props.children;
  }
}

export default ErrorBoundary;
