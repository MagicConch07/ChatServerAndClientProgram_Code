using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace BotManager
{
    // 설정 관리 클래스
    public class Config
    {
        public string BotExecutablePath { get; set; } = "";
        public Dictionary<string, string> BotPaths { get; set; } = new Dictionary<string, string>();

        public void LoadConfig()
        {
            string configFile = "config.txt";
            if (File.Exists(configFile))
            {
                string[] lines = File.ReadAllLines(configFile);
                foreach (string line in lines)
                {
                    if (line.Contains("="))
                    {
                        string[] parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            BotPaths[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }
            }
            else
            {
                // 기본 설정 파일 생성
                CreateDefaultConfig();
            }
        }

        private void CreateDefaultConfig()
        {
            string defaultConfig = @"# 봇 프로그램 경로 설정
# 형식: 봇이름=경로
GameBot=D:\repo\School\ChatServer\GGMClient\bin\x64\Debug\GGMClient.exe";

            File.WriteAllText("config.txt", defaultConfig);
            Console.WriteLine("기본 설정파일(config.txt)이 생성되었습니다. 봇 경로를 설정해주세요.");
        }

        public void ShowAvailableBots()
        {
            Console.WriteLine("\n=== 등록된 봇 목록 ===");
            foreach (var bot in BotPaths)
            {
                string status = File.Exists(bot.Value) ? "✓" : "✗";
                Console.WriteLine($"{status} {bot.Key}: {bot.Value}");
            }
            Console.WriteLine();
        }
    }

    // 프로세스 관리 클래스
    public class ProcessManager
    {
        private List<Process> activeProcesses = new List<Process>();
        private object lockObject = new object();

        public void StartBots(string botName, string serverIP, int count, string additionalArgs = "")
        {
            var config = new Config();
            config.LoadConfig();

            if (!config.BotPaths.ContainsKey(botName))
            {
                Console.WriteLine($"오류: '{botName}' 봇을 찾을 수 없습니다.");
                return;
            }

            string botPath = config.BotPaths[botName];
            if (!File.Exists(botPath))
            {
                Console.WriteLine($"오류: 봇 파일이 존재하지 않습니다: {botPath}");
                return;
            }

            Console.WriteLine($"\n{botName} 봇을 {count}개 시작합니다...");
            Console.WriteLine($"서버 IP: {serverIP}");
            Console.WriteLine($"추가 인자: {additionalArgs}");

            // 현재 활성 봇 수를 기준으로 ID 시작
            int startId = activeProcesses.Count + 1;

            // 봇 실행파일의 디렉토리 경로 가져오기
            string botDirectory = Path.GetDirectoryName(botPath);

            for (int i = 0; i < count; i++)
            {
                try
                {
                    string arguments = $"{serverIP} {startId + i} {additionalArgs}";

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = botPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = false,
                        WindowStyle = ProcessWindowStyle.Minimized,
                        WorkingDirectory = botDirectory  // 작업 디렉토리를 봇 실행파일 경로로 설정
                    };

                    Process process = Process.Start(startInfo);
                    if (process != null)
                    {
                        lock (lockObject)
                        {
                            activeProcesses.Add(process);
                        }
                        Console.WriteLine($"봇 {i + 1}/{count} 시작됨 (PID: {process.Id}, 작업경로: {botDirectory})");

                        // 프로세스 간 간격
                        Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"봇 {i + 1} 시작 실패: {ex.Message}");
                }
            }

            Console.WriteLine($"\n총 {count}개 봇 시작 완료!");
        }

        public void StopAllBots()
        {
            lock (lockObject)
            {
                Console.WriteLine($"\n{activeProcesses.Count}개의 활성 봇을 종료합니다...");

                int stoppedCount = 0;
                foreach (Process process in activeProcesses)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            process.WaitForExit(3000);
                            stoppedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"프로세스 종료 실패 (PID: {process.Id}): {ex.Message}");
                    }
                }

                activeProcesses.Clear();
                Console.WriteLine($"{stoppedCount}개 봇이 종료되었습니다.");
            }
        }

        public void ShowStatus()
        {
            lock (lockObject)
            {
                Console.WriteLine($"\n=== 봇 상태 ===");
                Console.WriteLine($"활성 프로세스: {activeProcesses.Count}개");

                int runningCount = 0;
                foreach (Process process in activeProcesses)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            runningCount++;
                            Console.WriteLine($"PID: {process.Id}, 메모리: {process.WorkingSet64 / 1024 / 1024}MB");
                        }
                    }
                    catch
                    {
                        // 프로세스가 이미 종료된 경우
                    }
                }
                Console.WriteLine($"실행 중: {runningCount}개");
            }
        }

        public void Cleanup()
        {
            // 종료되지 않은 프로세스들 정리
            lock (lockObject)
            {
                for (int i = activeProcesses.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        if (activeProcesses[i].HasExited)
                        {
                            activeProcesses.RemoveAt(i);
                        }
                    }
                    catch
                    {
                        activeProcesses.RemoveAt(i);
                    }
                }
            }
        }
    }

    // 메인 프로그램
    class Program
    {
        private static ProcessManager processManager = new ProcessManager();
        private static Config config = new Config();

        static void Main(string[] args)
        {
            Console.WriteLine("=== 간단한 봇 프로세스 관리자 ===\n");

            config.LoadConfig();

            // 명령어 도움말 표시
            ShowHelp();

            // 메인 루프
            while (true)
            {
                Console.Write("\n> ");
                string input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input))
                    continue;

                ProcessCommand(input);
            }
        }

        private static void ProcessCommand(string input)
        {
            string[] parts = input.Split(' ');
            string command = parts[0].ToLower();

            try
            {
                switch (command)
                {
                    case "start":
                        if (parts.Length < 4)
                        {
                            Console.WriteLine("사용법: start [봇이름] [서버IP] [개수] [추가인자...]");
                            break;
                        }
                        string botName = parts[1];
                        string serverIP = parts[2];
                        int count = int.Parse(parts[3]);
                        string additionalArgs = "";
                        if (parts.Length > 4)
                        {
                            additionalArgs = string.Join(" ", parts, 4, parts.Length - 4);
                        }
                        processManager.StartBots(botName, serverIP, count, additionalArgs);
                        break;

                    case "stop":
                        processManager.StopAllBots();
                        break;

                    case "status":
                        processManager.ShowStatus();
                        break;

                    case "list":
                        config.ShowAvailableBots();
                        break;

                    case "reload":
                        config.LoadConfig();
                        Console.WriteLine("설정을 다시 로드했습니다.");
                        break;

                    case "cleanup":
                        processManager.Cleanup();
                        Console.WriteLine("종료된 프로세스를 정리했습니다.");
                        break;

                    case "help":
                        ShowHelp();
                        break;

                    case "exit":
                    case "quit":
                        processManager.StopAllBots();
                        Environment.Exit(0);
                        break;

                    default:
                        Console.WriteLine($"알 수 없는 명령어: {command}");
                        Console.WriteLine("'help'를 입력하여 도움말을 확인하세요.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"명령어 실행 오류: {ex.Message}");
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("=== 사용 가능한 명령어 ===");
            Console.WriteLine("start [봇이름] [서버IP] [개수] [추가인자...] - 봇 시작");
            Console.WriteLine("stop                                      - 모든 봇 종료");
            Console.WriteLine("status                                    - 봇 상태 확인");
            Console.WriteLine("list                                      - 등록된 봇 목록");
            Console.WriteLine("reload                                    - 설정 다시 로드");
            Console.WriteLine("cleanup                                   - 종료된 프로세스 정리");
            Console.WriteLine("help                                      - 도움말 표시");
            Console.WriteLine("exit/quit                                 - 프로그램 종료");
            Console.WriteLine();
            Console.WriteLine("예제:");
            Console.WriteLine("start GameBot 192.168.1.100 5 user1 pass1");
            Console.WriteLine("start TestBot 127.0.0.1 3");
        }
    }
}
