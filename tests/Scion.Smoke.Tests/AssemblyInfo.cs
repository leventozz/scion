// Two desktop instances must not run at the same time: WebView2 refuses a shared user data folder
// that is opened with different browser arguments (each instance gets its own debugging port).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
