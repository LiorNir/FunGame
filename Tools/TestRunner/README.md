# Stand-in test runner

Builds and runs the EditMode tests in `Assets/Tests/EditMode` without opening Unity, using the compiler and runtime that
ship with the editor. `Runner.cs` is a tiny replacement for NUnit (only the few `Assert` methods the tests use), so a test
that needs another NUnit feature must either stay simple or be run from Unity's Test Runner.

Run `Tools\TestRunner\run-tests.bat` (set `UNITY_DATA` if Unity is not installed at the default place). It prints the failures
and a line like `80/80 passed`. The same tests run from Unity's Test Runner; both should agree.
