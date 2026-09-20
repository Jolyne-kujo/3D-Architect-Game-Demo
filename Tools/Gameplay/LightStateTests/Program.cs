using System;
using CoastalTemple.LightPuzzles;
class Program {
 static int failures;
 static void Test(string name, Action action) { try { action(); Console.WriteLine("PASS " + name); } catch(Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
 static void Check(bool value, string message) { if (!value) throw new Exception(message); }
 static void Main() {
  Test("permanent curtain requires continuous hit", () => { var s=new LightActivationState(); s.Step(true,.2f,.35f,.15f,true); Check(!s.Active,"activated before charge"); Check(s.Progress>.5f,"charge did not advance"); s.Step(false,.01f,.35f,.15f,true); s.Step(true,.2f,.35f,.15f,true); Check(!s.Active,"interrupted hit retained charge"); s.Step(true,.15f,.35f,.15f,true); Check(s.Active,"continuous charge did not activate"); });
  Test("permanent curtain survives complete light loss", () => {var s=new LightActivationState();s.Step(true,.35f,.35f,.15f,true);s.Step(false,99,.35f,.15f,true);Check(s.Active,"permanent state lost");});
  Test("temporary curtain opens immediately",()=>{var s=new LightActivationState();s.Step(true,0,0,.15f,false);Check(s.Active,"hit did not open");});
  Test("temporary curtain observes grace and restores",()=>{var s=new LightActivationState();s.Step(true,0,0,.15f,false);s.Step(false,.1f,0,.15f,false);Check(s.Active,"restored during grace");s.Step(false,.06f,0,.15f,false);Check(!s.Active,"failed to restore after grace");});
  Test("reillumination restarts grace",()=>{var s=new LightActivationState();s.Step(true,0,0,.15f,false);s.Step(false,.1f,0,.15f,false);s.Step(true,.01f,0,.15f,false);s.Step(false,.1f,0,.15f,false);Check(s.Active,"old light-loss time survived reillumination");});
  Test("explicit reset clears latched state and progress",()=>{var s=new LightActivationState();s.Step(true,1,.35f,.15f,true);Check(s.Active,"baseline inactive");s.Reset();Check(!s.Active&&s.Progress==0,"reset did not clear state");});
  Test("zero delta does not charge but preserves active",()=>{var s=new LightActivationState();s.Step(true,0,.35f,0,true);Check(!s.Active,"zero-time trace charged");s.Step(true,.35f,.35f,0,true);s.Step(false,0,.35f,0,true);Check(s.Active,"zero-time trace lost permanent state");});
  Environment.ExitCode=failures==0?0:1;
 }
}
