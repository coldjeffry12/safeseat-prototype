using System.Runtime.CompilerServices;

// The test executable can inject a fake backend; the production public API exposes no such seam.
[assembly: InternalsVisibleTo("SafeSeat.NativeTests")]
