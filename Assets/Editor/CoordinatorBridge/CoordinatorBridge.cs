using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

using UnityEngine;

using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;

namespace IronRing.Tools.Editor
{
    [InitializeOnLoad]
    public static class CoordinatorBridge
    {
        private const string JOB_KEY = "IronRing.Bridge.Job";
        private const string PHASE_KEY = "IronRing.Bridge.Phase";
        private const string ERRORS_KEY = "IronRing.Bridge.CompileErrors";
        private const string CAPTURE_KEY = "IronRing.Bridge.Capture";
        private const string STARTED_AT_KEY = "IronRing.Bridge.JobStartedUtc";
        /// <summary>진행 중 명령의 최대 허용 시간(초). 초과하면 실패로 기록하고 큐를 계속 처리한다.</summary>
        private const double JOB_TIMEOUT_SECONDS = 240;
        private static readonly string _root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".bridge"));
        private static Command _job;
        private static double _nextPoll;
        private static double _settleAt;
        private static double _nextProgressLog;
        private static TestRunnerApi _testApi;

        /// <summary>Editor 세션을 입력으로 이벤트를 연결하고 저장된 명령 상태를 복구한다.</summary>
        static CoordinatorBridge()
        {
            Directory.CreateDirectory(Path.Combine(_root, "commands"));
            Directory.CreateDirectory(Path.Combine(_root, "results"));
            Directory.CreateDirectory(Path.Combine(_root, "done"));
            string saved = SessionState.GetString(JOB_KEY, "");
            if (!string.IsNullOrEmpty(saved)) _job = Decode<Command>(saved);
            if (!SessionState.GetBool("IronRing.Bridge.Started", false))
            {
                foreach (string file in Directory.GetFiles(Path.Combine(_root, "commands"), "*.json"))
                    if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddMinutes(-10)) Archive(file);
                SessionState.SetBool("IronRing.Bridge.Started", true);
            }
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            EditorApplication.update += Update;
            _settleAt = EditorApplication.timeSinceStartup + 2;
            EditorApplication.delayCall += RegisterTestCallbacks;
            Log("Bridge loaded; pending=" + (_job?.Id ?? "none"));
        }

        /// <summary>입력 없이 테스트 API 콜백을 등록하여 재로드 후에도 결과를 수신한다.</summary>
        private static void RegisterTestCallbacks()
        {
            if (_testApi != null) return;
            _testApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            _testApi.RegisterCallbacks(new TestCallbacks());
        }

        /// <summary>진행 중 명령이 허용 시간을 넘겼는지 확인하고, 넘겼으면 Play 모드를 끝내고 실패로 기록한다.</summary>
        private static bool AbortIfTimedOut(string phase)
        {
            string startedAt = SessionState.GetString(STARTED_AT_KEY, "");
            if (!DateTime.TryParse(startedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime started))
            {
                // 시작 시각이 없는 명령은 이 감시 기능이 추가되기 전에 시작된 오래된 명령이다. 바로 정리한다.
                Log($"Stale job {_job.Id} {_job.Type} phase={phase} has no start timestamp; aborting it.");
                if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
                Finish(false, "", $"Stale command aborted after an editor reload in phase '{phase}'.");
                return true;
            }
            double elapsed = (DateTime.UtcNow - started).TotalSeconds;
            if (EditorApplication.timeSinceStartup >= _nextProgressLog)
            {
                _nextProgressLog = EditorApplication.timeSinceStartup + 30;
                Log($"In progress {_job.Id} {_job.Type} phase={phase} elapsed={elapsed:F0}s playing={EditorApplication.isPlaying}");
            }
            if (elapsed < JOB_TIMEOUT_SECONDS) return false;
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
            Finish(false, "", $"Command timed out after {elapsed:F0}s in phase '{phase}' (limit {JOB_TIMEOUT_SECONDS}s).");
            return true;
        }

        /// <summary>컴파일 컨텍스트를 받아 이전 오류 상태를 초기화한다.</summary>
        private static void OnCompilationStarted(object context) => SessionState.SetString(ERRORS_KEY, "");

        /// <summary>어셈블리와 진단 메시지를 받아 오류를 세션에 누적 저장한다.</summary>
        private static void OnAssemblyCompilationFinished(string assembly, CompilerMessage[] messages)
        {
            string errors = string.Join("\n", messages.Where(m => m.type == CompilerMessageType.Error)
                .Select(m => $"{m.file}({m.line},{m.column}): {m.message}"));
            if (errors.Length > 0)
                SessionState.SetString(ERRORS_KEY, SessionState.GetString(ERRORS_KEY, "") + errors + "\n");
        }

        /// <summary>컴파일 컨텍스트를 받아 완료 후 결과 판정 대기 시간을 갱신한다.</summary>
        private static void OnCompilationFinished(object context) => _settleAt = EditorApplication.timeSinceStartup + 2;

        /// <summary>Editor 시간을 사용하여 큐 또는 진행 중 명령 하나를 처리한다.</summary>
        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 1;
            try
            {
                if (_job != null) { ContinueJob(); return; }
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                string file = Directory.GetFiles(Path.Combine(_root, "commands"), "*.json")
                    .OrderBy(File.GetLastWriteTimeUtc).FirstOrDefault();
                if (file == null) return;
                Command command;
                try
                {
                    command = Decode<Command>(File.ReadAllText(file));
                    if (command == null || !Regex.IsMatch(command.Id ?? "", "^[A-Za-z0-9_-]{1,100}$") ||
                        Path.GetFileNameWithoutExtension(file) != command.Id)
                        throw new ArgumentException("Command id must match its filename and contain safe characters.");
                }
                catch (Exception ex) { Log("Invalid command " + file + ": " + ex.Message); Archive(file); return; }
                if (File.Exists(ResultPath(command.Id))) { Archive(file); return; }
                _job = command;
                SessionState.SetString(JOB_KEY, Encode(command));
                SessionState.SetString(PHASE_KEY, "executing");
                SessionState.SetString(STARTED_AT_KEY, DateTime.UtcNow.ToString("O"));
                _nextProgressLog = EditorApplication.timeSinceStartup + 30;
                Log("Begin " + command.Id + " " + command.Type);
                Execute();
            }
            catch (Exception ex)
            {
                if (_job != null) Finish(false, "", ex.ToString());
                else Log(ex.ToString());
            }
        }

        /// <summary>현재 명령의 유형과 인자를 사용하여 Editor 작업을 실행하거나 비동기 단계로 전환한다.</summary>
        private static void Execute()
        {
            if (_job.Type == "execute-method" || _job.Type == "open-scene" || _job.Type == "play-capture")
            {
                RequireEditMode();
                SessionState.SetString("IronRing.Bridge.OriginalScene", EditorSceneManager.GetActiveScene().path);
                if (string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path) && EditorSceneManager.GetActiveScene().isDirty)
                    throw new InvalidOperationException("Untitled dirty scene cannot be saved or restored without a dialog; save it manually first.");
                if (_job.SaveScene ?? true) SaveScenesWithoutDialog();
            }
            switch (_job.Type)
            {
                case "ping": Finish(true, "pong; Unity " + Application.unityVersion); break;
                case "scene-status":
                    Finish(true, Encode(new SceneStatus { Path = EditorSceneManager.GetActiveScene().path,
                        IsDirty = EditorSceneManager.GetActiveScene().isDirty, Playing = EditorApplication.isPlaying }));
                    break;
                case "compile-status": Finish(true, CompileStatus()); break;
                case "refresh":
                    RequireEditMode();
                    SessionState.SetString(PHASE_KEY, "refresh");
                    _settleAt = EditorApplication.timeSinceStartup + 3;
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    break;
                case "execute-method":
                    RequireEditMode();
                    int separator = (_job.Method ?? "").LastIndexOf('.');
                    if (separator < 1) throw new ArgumentException("Expected Namespace.Type.Method");
                    string typeName = _job.Method.Substring(0, separator);
                    Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
                    MethodInfo method = type?.GetMethod(_job.Method.Substring(separator + 1),
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                    if (method == null) throw new MissingMethodException(_job.Method);
                    if (method.ReturnType != typeof(void) && method.ReturnType != typeof(string))
                        throw new ArgumentException("Only synchronous void/string methods are supported.");
                    object result = method.Invoke(null, null);
                    SessionState.SetString("IronRing.Bridge.MethodOutput", result?.ToString() ?? "Executed " + _job.Method);
                    SessionState.SetString(PHASE_KEY, "method-complete");
                    _settleAt = EditorApplication.timeSinceStartup + 2;
                    break;
                case "run-tests":
                    RequireEditMode();
                    if (EditorUtility.scriptCompilationFailed) throw new InvalidOperationException(CompileStatus());
                    PrepareOutputPath(_job.Results);
                    SessionState.SetString(PHASE_KEY, "tests");
                    RegisterTestCallbacks();
                    _testApi.Execute(new ExecutionSettings(new Filter
                    {
                        testMode = TestMode.EditMode,
                        groupNames = string.IsNullOrWhiteSpace(_job.Filter) ? null : new[] { "^" + Regex.Escape(_job.Filter) + "(?:\\.|$|\\()" }
                    }));
                    break;
                case "open-scene": RequireEditMode(); OpenScene(_job.Path); Finish(true, _job.Path); break;
                case "play-capture":
                    RequireEditMode();
                    if (EditorUtility.scriptCompilationFailed) throw new InvalidOperationException(CompileStatus());
                    if (!string.IsNullOrEmpty(_job.Path)) OpenScene(_job.Path);
                    PrepareOutputPath(_job.Screenshot);
                    if (File.Exists(_job.Screenshot)) throw new IOException("Screenshot already exists; choose a new output path.");
                    if (_job.AfterFrames < 0 || _job.Seconds < 0) throw new ArgumentException("Capture delay must be nonnegative.");
                    SessionState.SetString(CAPTURE_KEY + ".Path", _job.Screenshot);
                    SessionState.SetInt(CAPTURE_KEY + ".Frames", _job.AfterFrames);
                    SessionState.SetFloat(CAPTURE_KEY + ".Seconds", _job.Seconds);
                    SessionState.SetString(CAPTURE_KEY + ".ExtraArgs", _job.ExtraArgs ?? "");
                    SessionState.SetBool(CAPTURE_KEY + ".Armed", true);
                    SessionState.SetBool(CAPTURE_KEY + ".Done", false);
                    SessionState.SetString(CAPTURE_KEY + ".Error", "");
                    SessionState.SetString(PHASE_KEY, "enter-play");
                    EditorApplication.EnterPlaymode();
                    break;
                case "stop":
                    SessionState.SetString(PHASE_KEY, "stop");
                    EditorApplication.ExitPlaymode();
                    break;
                default: throw new ArgumentException("Unknown command type: " + _job.Type);
            }
        }

        /// <summary>저장된 진행 단계를 사용하여 컴파일 또는 Play 전환 이후 결과를 완성한다.</summary>
        private static void ContinueJob()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < _settleAt) return;
            string phase = SessionState.GetString(PHASE_KEY, "");
            if (AbortIfTimedOut(phase)) return;
            switch (phase)
            {
                case "refresh": Finish(!EditorUtility.scriptCompilationFailed, CompileStatus(), SessionState.GetString(ERRORS_KEY, "")); break;
                case "method-complete": Finish(true, SessionState.GetString("IronRing.Bridge.MethodOutput", "")); break;
                case "enter-play":
                case "capture":
                    if (EditorApplication.isPlaying)
                    {
                        SessionState.SetString(PHASE_KEY, "capture");
                        if (SessionState.GetBool(CAPTURE_KEY + ".Done", false))
                        {
                            SessionState.SetString(PHASE_KEY, "exit-play");
                            EditorApplication.ExitPlaymode();
                        }
                    }
                    else if (!EditorApplication.isPlayingOrWillChangePlaymode)
                        Finish(false, "", "Play mode ended before capture completed.");
                    break;
                case "exit-play":
                    if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        string error = SessionState.GetString(CAPTURE_KEY + ".Error", "");
                        bool exists = File.Exists(_job.Screenshot) && new FileInfo(_job.Screenshot).Length > 8;
                        Finish(exists && error.Length == 0, _job.Screenshot, error.Length > 0 ? error : exists ? "" : "Screenshot was not written.");
                    }
                    break;
                case "stop": if (!EditorApplication.isPlayingOrWillChangePlaymode) Finish(true, "Edit mode"); break;
                case "tests": break;
                default: Finish(false, "", "Domain reload interrupted execution; command was not repeated."); break;
            }
        }

        /// <summary>입력 없이 현재 컴파일 실패 여부와 수집한 오류를 JSON 문자열로 반환한다.</summary>
        private static string CompileStatus() => Encode(new CompilationStatus
        {
            Compiling = EditorApplication.isCompiling,
            Failed = EditorUtility.scriptCompilationFailed,
            Errors = SessionState.GetString(ERRORS_KEY, "")
        });

        /// <summary>입력 없이 현재 Play 상태를 확인하며 Edit 모드가 아니면 예외를 반환한다.</summary>
        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Command requires Edit mode.");
        }

        /// <summary>프로젝트 씬 경로를 검증하고 미저장 씬이 없을 때만 해당 씬을 연다.</summary>
        private static void OpenScene(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || path.Contains("..") || !File.Exists(path))
                throw new ArgumentException("Expected an existing Assets/...unity scene path.");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene changes; save them before opening another scene.");
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        /// <summary>절대 출력 경로를 검증하고 파일의 부모 폴더를 생성한다.</summary>
        private static void PrepareOutputPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) throw new ArgumentException("Output path must be absolute.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
        }

        /// <summary>열린 씬을 검사하고 경로가 있는 dirty 씬만 모달 없이 저장한다.</summary>
        private static void SaveScenesWithoutDialog()
        {
            bool dirty = false;
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isDirty) continue;
                if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Untitled dirty scene cannot be saved without a dialog; save it manually first.");
                dirty = true;
            }
            if (dirty && !EditorSceneManager.SaveOpenScenes()) throw new IOException("Could not save open scenes.");
        }

        /// <summary>명령 식별자를 사용하여 결과 JSON 파일 경로를 반환한다.</summary>
        private static string ResultPath(string id) => Path.Combine(_root, "results", id + ".json");

        /// <summary>성공 여부와 출력 및 오류를 원자적으로 저장하고 명령을 보관 처리한다.</summary>
        private static void Finish(bool ok, string output, string error = "")
        {
            if ((_job.RestoreScene ?? true) && (_job.Type == "execute-method" || _job.Type == "open-scene" || _job.Type == "play-capture"))
            {
                try
                {
                    string original = SessionState.GetString("IronRing.Bridge.OriginalScene", "");
                    if (!string.IsNullOrEmpty(original) && EditorSceneManager.GetActiveScene().path != original)
                    {
                        RequireEditMode();
                        if (_job.SaveScene ?? true) SaveScenesWithoutDialog();
                        OpenScene(original);
                    }
                }
                catch (Exception ex) { ok = false; error += "\nScene restoration failed: " + ex.Message; }
            }
            string id = _job.Id;
            string destination = ResultPath(id);
            string temp = destination + ".tmp";
            File.WriteAllText(temp, Encode(new Result { Id = id, Ok = ok, Output = output, Error = error, FinishedAt = DateTime.UtcNow.ToString("O") }));
            if (!File.Exists(destination)) File.Move(temp, destination); else File.Delete(temp);
            Archive(Path.Combine(_root, "commands", id + ".json"));
            SessionState.EraseString(JOB_KEY);
            SessionState.EraseString(PHASE_KEY);
            SessionState.EraseString(STARTED_AT_KEY);
            SessionState.EraseString("IronRing.Bridge.OriginalScene");
            SessionState.SetBool(CAPTURE_KEY + ".Armed", false);
            _job = null;
            Log("Finished " + id + " ok=" + ok + (ok ? "" : " " + error));
        }

        /// <summary>처리한 파일 경로를 받아 done 폴더로 이동하며 기존 보관 파일을 보존한다.</summary>
        private static void Archive(string file)
        {
            if (!File.Exists(file)) return;
            string target = Path.Combine(_root, "done", Path.GetFileName(file));
            if (File.Exists(target)) target += "." + Guid.NewGuid().ToString("N");
            File.Move(file, target);
        }

        /// <summary>메시지를 받아 UTC 시각과 함께 브리지 로그에 추가한다.</summary>
        private static void Log(string message) => File.AppendAllText(Path.Combine(_root, "bridge.log"), DateTime.UtcNow.ToString("O") + " " + message + "\n");

        /// <summary>JSON 문자열을 받아 지정한 데이터 계약 객체를 반환한다.</summary>
        private static T Decode<T>(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }

        /// <summary>데이터 계약 객체를 받아 UTF8 JSON 문자열을 반환한다.</summary>
        private static string Encode<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private sealed class TestCallbacks : IErrorCallbacks
        {
            /// <summary>테스트 트리를 수신하며 실행 상태는 TestRunnerApi에 유지한다.</summary>
            public void RunStarted(ITestAdaptor testsToRun) { }
            /// <summary>개별 테스트 시작 정보를 수신하며 별도 상태를 변경하지 않는다.</summary>
            public void TestStarted(ITestAdaptor test) { }
            /// <summary>개별 테스트 결과를 수신하며 최종 결과 트리에서 함께 저장한다.</summary>
            public void TestFinished(ITestResultAdaptor result) { }
            /// <summary>실행 오류 메시지를 받아 현재 테스트 명령을 실패로 종료한다.</summary>
            public void OnError(string message)
            {
                if (_job?.Type == "run-tests") Finish(false, "", message);
            }
            /// <summary>최종 결과 트리를 받아 NUnit XML과 통과 실패 전체 개수 요약을 저장한다.</summary>
            public void RunFinished(ITestResultAdaptor result)
            {
                if (_job?.Type != "run-tests") return;
                try
                {
                    TestRunnerApi.SaveResultToFile(result, _job.Results);
                    int total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
                    string summary = $"passed={result.PassCount}; failed={result.FailCount}; total={total}; skipped={result.SkipCount}; results={_job.Results}";
                    bool ok = total > 0 && result.FailCount == 0 && result.TestStatus == TestStatus.Passed;
                    Finish(ok, summary, ok ? "" : "Tests failed, were inconclusive, or no tests matched. " + result.Message);
                }
                catch (Exception ex) { Finish(false, "", ex.ToString()); }
            }
        }

        [DataContract]
        private sealed class Command
        {
            [DataMember(Name = "id")] public string Id { get; set; }
            [DataMember(Name = "type")] public string Type { get; set; }
            [DataMember(Name = "method")] public string Method { get; set; }
            [DataMember(Name = "filter")] public string Filter { get; set; }
            [DataMember(Name = "results")] public string Results { get; set; }
            [DataMember(Name = "path")] public string Path { get; set; }
            [DataMember(Name = "screenshot")] public string Screenshot { get; set; }
            [DataMember(Name = "seconds")] public float Seconds { get; set; }
            [DataMember(Name = "afterFrames")] public int AfterFrames { get; set; }
            [DataMember(Name = "extraArgs")] public string ExtraArgs { get; set; }
            [DataMember(Name = "saveScene")] public bool? SaveScene { get; set; }
            [DataMember(Name = "restoreScene")] public bool? RestoreScene { get; set; }
        }

        [DataContract]
        private sealed class Result
        {
            [DataMember(Name = "id")] public string Id { get; set; }
            [DataMember(Name = "ok")] public bool Ok { get; set; }
            [DataMember(Name = "output")] public string Output { get; set; }
            [DataMember(Name = "error")] public string Error { get; set; }
            [DataMember(Name = "finishedAt")] public string FinishedAt { get; set; }
        }

        [DataContract]
        private sealed class CompilationStatus
        {
            [DataMember(Name = "compiling")] public bool Compiling { get; set; }
            [DataMember(Name = "failed")] public bool Failed { get; set; }
            [DataMember(Name = "errors")] public string Errors { get; set; }
        }

        [DataContract]
        private sealed class SceneStatus
        {
            [DataMember(Name = "path")] public string Path { get; set; }
            [DataMember(Name = "isDirty")] public bool IsDirty { get; set; }
            [DataMember(Name = "playing")] public bool Playing { get; set; }
        }
    }
}
