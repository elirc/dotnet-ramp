"""Compile copied lifecycle source in disposable directories; never alter the app."""
from pathlib import Path
import hashlib
import json
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[3]
SOURCES = [ROOT / "backend/Domain/Enums.cs", ROOT / "backend/Domain/ExpenseStateMachine.cs"]
def hashes():
    return {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in SOURCES}

PROGRAM = r"""
using ExpenseManagement.Api.Domain;
var statuses = Enum.GetValues<ExpenseStatus>();
var roles = Enum.GetValues<UserRole>();
var human = new HashSet<(ExpenseStatus, ExpenseStatus, UserRole)> {
    (ExpenseStatus.Draft, ExpenseStatus.Submitted, UserRole.Employee),
    (ExpenseStatus.Rejected, ExpenseStatus.Submitted, UserRole.Employee),
    (ExpenseStatus.Submitted, ExpenseStatus.Approved, UserRole.Manager),
    (ExpenseStatus.Submitted, ExpenseStatus.Rejected, UserRole.Manager),
    (ExpenseStatus.Approved, ExpenseStatus.Reimbursing, UserRole.Finance)
};
var edges = human.Select(x => (x.Item1, x.Item2)).ToHashSet();
edges.Add((ExpenseStatus.Reimbursing, ExpenseStatus.Reimbursed));
var checks = 0;
void Check(bool condition, string label) {
    checks++;
    if (!condition) throw new Exception("SEMANTIC FAILURE: " + label);
}
foreach (var from in statuses) {
    foreach (var to in statuses) {
        foreach (var role in roles)
            Check(ExpenseStateMachine.CanTransition(from,to,role) == human.Contains((from,to,role)), $"human {from}->{to} {role}");
        Check(ExpenseStateMachine.CanSystemTransition(from,to) == edges.Contains((from,to)), $"system {from}->{to}");
    }
    Check(ExpenseStateMachine.NextStates(from).ToHashSet().SetEquals(edges.Where(x => x.Item1 == from).Select(x => x.Item2)), $"next states {from}");
    Check(ExpenseStateMachine.IsTerminal(from) == (from == ExpenseStatus.Reimbursed), $"terminal {from}");
}
Console.WriteLine($"PASS {checks} assertions");
"""

def run_case(label, old=None, new=None):
    with tempfile.TemporaryDirectory(prefix="netramp-lifecycle-") as raw:
        temp = Path(raw)
        for source in SOURCES:
            shutil.copyfile(source, temp / source.name)
        if old is not None:
            path = temp / "ExpenseStateMachine.cs"
            content = path.read_text(encoding="utf-8-sig")
            if content.count(old) != 1:
                raise RuntimeError(f"Mutation anchor changed: {label}")
            path.write_text(content.replace(old, new), encoding="utf-8")
        (temp / "Lab.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>', encoding="utf-8")
        (temp / "Program.cs").write_text(PROGRAM, encoding="utf-8")
        build = subprocess.run(["dotnet", "build", "Lab.csproj", "--nologo", "-v:q"], cwd=temp, capture_output=True, text=True)
        if build.returncode:
            raise RuntimeError(f"Build failed for {label}: {build.stdout}\n{build.stderr}")
        run = subprocess.run(["dotnet", "run", "--project", "Lab.csproj", "--no-build"], cwd=temp, capture_output=True, text=True)
        output = run.stdout + run.stderr
        expected = run.returncode == 0 and "PASS 156 assertions" in output if old is None else run.returncode != 0 and "SEMANTIC FAILURE:" in output
        if not expected:
            raise RuntimeError(f"Unexpected outcome {label}: {output}")
        return {"case": label, "build_exit": build.returncode, "execution_exit": run.returncode, "evidence": next(line for line in output.splitlines() if "PASS " in line or "SEMANTIC FAILURE:" in line)}

def main():
    before = hashes()
    cases = [run_case("baseline"),
        run_case("approval-role", "[ExpenseStatus.Approved] = new[] { UserRole.Manager }", "[ExpenseStatus.Approved] = new[] { UserRole.Finance }"),
        run_case("narrow-system-helper", "Transitions.TryGetValue(from, out var map) && map.ContainsKey(to);", "Transitions.TryGetValue(from, out var map) && map.TryGetValue(to, out var roles) && roles.Length == 0;")]
    after = hashes()
    if before != after:
        raise RuntimeError("Original source hashes changed")
    print(json.dumps({"scope": "Copied actual enums and lifecycle table only", "cases": cases, "source_hashes": after, "originals_unchanged": True}, indent=2))

if __name__ == "__main__":
    main()
