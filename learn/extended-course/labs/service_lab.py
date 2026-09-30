"""Actual copied ExpenseService + collaborators; job factory is a declared stub."""
from pathlib import Path
import hashlib,json,shutil,subprocess,tempfile
ROOT=Path(__file__).resolve().parents[3]
REL=['Domain/Enums.cs','Domain/Expense.cs','Domain/Receipt.cs','Domain/User.cs','Domain/ExpenseStateMachine.cs','Domain/DomainExceptions.cs','Dtos/Dtos.cs','Storage/DataStore.cs','Services/PolicyEngine.cs','Services/ExpenseService.cs','Services/Mappings.cs','Background/BackgroundTaskQueue.cs']
SOURCES=[ROOT/'backend'/r for r in REL]
PROGRAM=r"""
using ExpenseManagement.Api.Domain;
using ExpenseManagement.Api.Dtos;
using ExpenseManagement.Api.Services;
using ExpenseManagement.Api.Storage;
using ExpenseManagement.Api.Background;
var checks=0;
void Check(bool value,string label){checks++;if(!value)throw new Exception("SEMANTIC FAILURE: "+label);}
void Throws<T>(Action action,string label) where T:Exception {bool caught=false;try{action();}catch(T){caught=true;}Check(caught,label);}
User Person(UserRole role,string name)=>new(){Email=name+"@example.test",FullName=name,PasswordHash="",PasswordSalt="",Role=role};
var owner=Person(UserRole.Employee,"Owner");var manager=Person(UserRole.Manager,"Manager");var finance=Person(UserRole.Finance,"Finance");owner.ManagerId=manager.Id;
var store=new DataStore();foreach(var u in new[]{owner,manager,finance})store.Users[u.Id]=u;
var queue=new GateQueue();var service=new ExpenseService(store,new PolicyEngine(),queue);
Expense Add(User who,ExpenseStatus status,decimal amount=10m){var e=new Expense{EmployeeId=who.Id,Status=status,Amount=amount,Description="Valid synthetic description",Merchant="M",Currency="USD",Category=ExpenseCategory.Other,ExpenseDate=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),UpdatedAt=DateTimeOffset.Parse("2000-01-01T00:00:00Z")};store.Expenses[e.Id]=e;return e;}
var approved=Add(owner,ExpenseStatus.Approved);approved.RejectionReason="old";var oldTime=approved.UpdatedAt;
Throws<DomainException>(()=>service.Reject(manager,approved.Id," new "),"invalid rejection throws");
Check(approved.RejectionReason=="new","rejection reason changes before failure");Check(approved.Status==ExpenseStatus.Approved,"rejection status unchanged");Check(approved.UpdatedAt==oldTime,"rejection timestamp unchanged");Check(approved.History.Count==0,"rejection history unchanged");
var blocked=Add(owner,ExpenseStatus.Draft,30m);var oldFlags=blocked.PolicyFlags;
Throws<DomainException>(()=>service.Submit(owner,blocked.Id),"missing receipt blocks submit");
Check(blocked.PolicyFlags.Any(f=>f.Code=="RECEIPT_REQUIRED"&&f.Severity==PolicySeverity.Blocking),"blocking receipt flag");Check(!ReferenceEquals(oldFlags,blocked.PolicyFlags),"failed submit refreshes flag list");Check(blocked.Status==ExpenseStatus.Draft&&blocked.History.Count==0,"blocked submit preserves lifecycle");
var pending=Add(owner,ExpenseStatus.Draft,30m);var receipt=new Receipt{ExpenseId=pending.Id,StoredFileName="synthetic.png",OriginalFileName="synthetic.png",ContentType="image/png",Status=ReceiptStatus.Pending};store.Receipts[receipt.Id]=receipt;pending.ReceiptId=receipt.Id;
service.Submit(owner,pending.Id);Check(pending.Status==ExpenseStatus.Submitted,"pending receipt permits submit");Check(pending.PolicyFlags.Any(f=>f.Code=="RECEIPT_PROCESSING"&&f.Severity==PolicySeverity.Warning),"pending receipt warning");Check(pending.History.Count==1&&pending.History[0].From==ExpenseStatus.Draft&&pending.History[0].To==ExpenseStatus.Submitted,"submission history edge");
var managerOwn=Add(manager,ExpenseStatus.Submitted);
Throws<ForbiddenException>(()=>service.Approve(manager,managerOwn.Id),"manager cannot approve own without report relation");Check(managerOwn.Status==ExpenseStatus.Submitted&&managerOwn.History.Count==0,"denied own approval unchanged");
var managerDraft=Add(manager,ExpenseStatus.Draft);Check(managerDraft.ToResponse(store,manager).AvailableActions.Contains("submit"),"current mapping offers owner submit hint");Throws<DomainException>(()=>service.Submit(manager,managerDraft.Id),"actual manager role cannot submit");
var reimbursing=Add(owner,ExpenseStatus.Approved);service.StartReimbursement(finance,reimbursing.Id);Check(queue.Calls==1,"queue invoked once");Check(!queue.Gate.Task.IsCompleted,"service returns before queue acceptance");Check(reimbursing.Status==ExpenseStatus.Reimbursing,"status changed before acceptance");Check(reimbursing.History.Count==1,"request history exists before acceptance");queue.Gate.SetResult();
var created=service.Create(owner,new CreateExpenseRequest{Merchant=" M ",Description=" D ",Amount=1m,Currency=" usd ",Category=ExpenseCategory.Other,ExpenseDate=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1)});Check(created.Merchant=="M"&&created.Description=="D","merchant and description trimmed");Check(created.Currency==" USD ","currency uppercased without trim in direct service call");
Console.WriteLine($"PASS {checks} assertions");
class GateQueue:IBackgroundTaskQueue{public int Calls;public TaskCompletionSource Gate=new(TaskCreationOptions.RunContinuationsAsynchronously);public ValueTask EnqueueAsync(Func<IServiceProvider,CancellationToken,Task> work){Calls++;return new ValueTask(Gate.Task);}public ValueTask<Func<IServiceProvider,CancellationToken,Task>> DequeueAsync(CancellationToken ct)=>throw new NotSupportedException();}
"""
STUB='namespace ExpenseManagement.Api.Background; public static class BackgroundJobs { public static Func<IServiceProvider,CancellationToken,Task> ProcessReimbursement(Guid id) => (_,_) => Task.CompletedTask; }'
def hashes():return {str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in SOURCES}
def case(label,mutation=False):
 with tempfile.TemporaryDirectory(prefix='netramp-service-') as raw:
  t=Path(raw)
  for p in SOURCES:shutil.copyfile(p,t/p.name)
  if mutation:
   p=t/'ExpenseService.cs';s=p.read_text(encoding='utf-8-sig');old='actor.Role != UserRole.Manager || !IsDirectReport(expense.EmployeeId, actor.Id)'
   if s.count(old)!=1:raise RuntimeError('Mutation anchor changed')
   p.write_text(s.replace(old,'actor.Role != UserRole.Manager'),encoding='utf-8')
  (t/'Program.cs').write_text(PROGRAM,encoding='utf-8');(t/'JobFactoryStub.cs').write_text(STUB,encoding='utf-8')
  (t/'Lab.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>',encoding='utf-8')
  build=subprocess.run(['dotnet','build','Lab.csproj','--nologo','-v:q'],cwd=t,capture_output=True,text=True)
  if build.returncode:raise RuntimeError(build.stdout+build.stderr)
  run=subprocess.run(['dotnet','run','--project','Lab.csproj','--no-build'],cwd=t,capture_output=True,text=True);output=run.stdout+run.stderr
  expected=run.returncode!=0 and 'SEMANTIC FAILURE: manager cannot approve own without report relation' in output if mutation else run.returncode==0 and 'PASS 22 assertions' in output
  if not expected:raise RuntimeError(output)
  return {'case':label,'build_exit':0,'execution_exit':run.returncode,'evidence':next(line for line in output.splitlines() if 'PASS ' in line or 'SEMANTIC FAILURE:' in line)}
def main():
 before=hashes();results=[case('baseline'),case('removed-manager-relationship',True)];after=hashes()
 if before!=after:raise RuntimeError('Original source changed')
 print(json.dumps({'scope':'Copied actual service/domain/policy/mapping/store; controlled queue; stub job factory, no worker/HTTP','cases':results,'source_hashes':after,'originals_unchanged':True},indent=2))
if __name__=='__main__':main()
