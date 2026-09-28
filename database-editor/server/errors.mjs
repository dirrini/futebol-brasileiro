export class EditorError extends Error {
  constructor(status, code, message, issues = []) {
    super(message);
    Object.assign(this, { status, code, issues });
  }
}
