"""Bounded test of copied actual queue source, not the expense service or host."""
from pathlib import Path
import hashlib
import json
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "backend/Background/BackgroundTaskQueue.cs"
PROGRAM = r"""
using ExpenseManagement.Api.Background;
var checks = 0;
void Check(bool condition, string label) {
    checks++;
    if (!condition) throw new Exception("SEMANTIC FAILURE: " + label);
}
var queue = new BackgroundTaskQueue(1);
Func<IServiceProvider,CancellationToken,Task> first = (_,_) => Task.CompletedTask;
Func<IServiceProvider,CancellationToken,Task> second = (_,_) => Task.FromResult(1);
var accepted = queue.EnqueueAsync(first);
Check(accepted.IsCompletedSuccessfully, "empty queue accepts first item");
await accepted;
var pending = queue.EnqueueAsync(second).AsTask();
Check(!pending.IsCompleted, "full queue waits for capacity");
var receivedFirst = await queue.DequeueAsync(CancellationToken.None);
Check(ReferenceEquals(receivedFirst,first), "first delegate identity");
await pending.WaitAsync(TimeSpan.FromSeconds(5));
Check(pending.IsCompletedSuccessfully, "pending write completes after dequeue");
var receivedSecond = await queue.DequeueAsync(CancellationToken.None);
Check(ReferenceEquals(receivedSecond,second), "second delegate identity");
using var canceled = new CancellationTokenSource();
canceled.Cancel();
var cancellationObserved = false;
try { await queue.DequeueAsync(canceled.Token); }
catch (OperationCanceledException) { cancellationObserved = true; }
Check(cancellationObserved, "empty read observes cancellation");
var nullRejected = false;
try { await queue.EnqueueAsync(null!); }
catch (ArgumentNullException) { nullRejected = true; }
Check(nullRejected, "null work rejected");
Console.WriteLine($"PASS {checks} assertions");
"""

def execute(label, mutate):
    with tempfile.TemporaryDirectory(prefix="netramp-queue-") as raw:
        temp = Path(raw)
        content = SOURCE.read_text(encoding="utf-8-sig")
        if mutate:
            anchor = "FullMode = BoundedChannelFullMode.Wait"
            if content.count(anchor) != 1:
                raise RuntimeError("Queue mutation anchor changed")
            content = content.replace(anchor,"FullMode = BoundedChannelFullMode.DropWrite")
        (temp/"BackgroundTaskQueue.cs").write_text(content,encoding="utf-8")
        (temp/"Program.cs").write_text(PROGRAM,encoding="utf-8")
        (temp/"Lab.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>',encoding="utf-8")
        build = subprocess.run(["dotnet","build","Lab.csproj","--nologo","-v:q"],cwd=temp,capture_output=True,text=True)
        if build.returncode:
            raise RuntimeError(build.stdout+build.stderr)
        result = subprocess.run(["dotnet","run","--project","Lab.csproj","--no-build"],cwd=temp,capture_output=True,text=True)
        output = result.stdout+result.stderr
        expected = result.returncode != 0 and "SEMANTIC FAILURE: full queue waits for capacity" in output if mutate else result.returncode == 0 and "PASS 7 assertions" in output
        if not expected:
            raise RuntimeError(output)
        return {"case":label,"build_exit":0,"execution_exit":result.returncode,"evidence":next(line for line in output.splitlines() if "PASS " in line or "SEMANTIC FAILURE:" in line)}

def main():
    before = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
    cases = [execute("baseline",False),execute("drop-instead-of-wait",True)]
    after = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
    if before != after:
        raise RuntimeError("Original queue source changed")
    print(json.dumps({"scope":"Copied actual bounded queue only; delegates not executed", "cases":cases,"original_unchanged":True,"source_sha256":after},indent=2))
if __name__ == "__main__":
    main()
