// Module to attach a global window keydown listener and forward events to .NET
let _handler = null;
let _attached = false;

export function register(dotNetRef) {
  if (_attached) return;

  _handler = (e) => {
    try {
      const key = (e.key || '').toLowerCase();
      const code = (e.code || '').toLowerCase();

      // Prevent default for navigation-affecting keys to mimic Blazor preventDefault
      if (
        key === ' ' || key === 'space' || key === 'spacebar' || code === 'space' ||
        key === 'arrowup' || key === 'arrowdown' || key === 'arrowleft' || key === 'arrowright' ||
        key === 'w' || key === 'a' || key === 's' || key === 'd' ||
        key === 'z' || key === 'q'
      ) {
        e.preventDefault?.();
        e.stopPropagation?.();
      }

      // Forward to .NET handler
      dotNetRef?.invokeMethodAsync('OnWindowKeyDown', key, code);
    } catch {
      // swallow errors to avoid breaking global handler
    }
  };

  window.addEventListener('keydown', _handler, true);
  _attached = true;
}

export function unregister() {
  if (!_attached) return;
  if (_handler) {
    window.removeEventListener('keydown', _handler, true);
  }
  _handler = null;
  _attached = false;
}
