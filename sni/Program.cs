using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Diagnostics;

namespace sni
{
    class Program
    {
        class Config
        {
            // 要测试的所有 IP 列表
            public List<IPAddress> ip = new List<IPAddress>();
            // 避免触发防火墙 SNI 阻断（如 google），避免明显的 CDN 服务器
            public string host = "github.com";

            // 建立 socket 连接超时，比 ping 略长即可
            public int timeout_connect = 500;
            // SNI 握手超时，可能需要较长时间
            public int timeout_auth = 3000;

            // 结果输出文件名，默认 "sni_yyyyMMdd_HHmmss.txt"
            public string result_filename;

            // 线程数，默认 20
            public int parallels = 20;

            // 重试次数，默认 2
            public int retry = 2;

            public Config()
            {
                result_filename = "sni_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
            }
        }

        class RestoreTitle
        {
            private string _title;
            public RestoreTitle()
            {
                _title = Console.Title;
            }
            ~RestoreTitle()
            {
                Console.Title = _title;
            }
        }

        static void Main(string[] args)
        {
            var conf = ParseArgs(args);
            if (conf == null)
            {
                return;
            }

            RestoreTitle restoreTitle = new RestoreTitle();


            Console.WriteLine("正在扫描 ...");

            var t1 = DateTime.Now;
            var of = File.Open(conf.result_filename, FileMode.Create, FileAccess.Write, FileShare.Read);
            var sw = new StreamWriter(of);

            var cur = 0;
            var len = conf.ip.Count();
            var result = 0;

            var cts = new CancellationTokenSource();
            var updateTitleTask = Task.Run(() => {
                do
                {
                    var p = (float)cur / len;
                    Console.Title = string.Format("{0} / {1} = {2:P2} | 剩余时间：{3:F0} 秒", cur, len, p, (DateTime.Now - t1).TotalSeconds / p * (1-p));
                } while (!cts.Token.WaitHandle.WaitOne(1000));
            }, cts.Token);
            
            var options = new ParallelOptions();
            options.MaxDegreeOfParallelism = conf.parallels;
            Parallel.ForEach(conf.ip, options, (ip) => {
                Interlocked.Increment(ref cur);
                Debug.WriteLine(ip.ToString());
                var ret = SNIDetect.Detect(ip, conf.host, conf.timeout_connect, conf.timeout_auth, conf.retry);
                if (ret == true)
                {
                    lock (sw)
                    {
                        result++;
                        Console.WriteLine(ip.ToString());
                        sw.WriteLine(ip.ToString());
                        sw.Flush();
                    }
                }
            });
            cts.Cancel();
            updateTitleTask.Wait();
            sw.Close();
            of.Close();

            Console.WriteLine("完成！总计 {0}/{1}，耗时 {2:F2} 秒", result, len, (DateTime.Now - t1).TotalSeconds);
        }

        static uint IPToInt(IPAddress ip)
        {
            if (ip.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException();
            }
            var ipv4bytes = ip.GetAddressBytes();
            var address = ((uint)ipv4bytes[0] << 24)
                + ((uint)ipv4bytes[1] << 16)
                + ((uint)ipv4bytes[2] << 8)
                + ipv4bytes[3];
            return address;
        }

        static IPAddress IntToIP(uint addr)
        {
            var result = BitConverter.GetBytes(addr);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);
            return new IPAddress(result);
        }

        static void Help()
        {
            var exe = Process.GetCurrentProcess().ProcessName;
            Console.WriteLine("参数：");
            Console.WriteLine("{0,-30}{1}", "-n, --host <host>", "默认 github.com，注意避开被墙域名和存在大量 CDN 的域名");
            Console.WriteLine("{0,-30}{1}", "-t, --timeout <conn> <auth>", "连接超时和握手超时，默认 500ms 和 3000ms");
            Console.WriteLine("{0,-34}{1}", "", "连接超时：建立与待测 IP 的原始连接，仅需比 ping 略高");
            Console.WriteLine("{0,-34}{1}", "", "握手超时：可能需要较长时间，以便代理服务器与目标域名建立连接");
            Console.WriteLine("{0,-30}{1}", "-p, --parallels <n>", "并行任务数，默认 20");
            Console.WriteLine("{0,-30}{1}", "-r, --retry <n>", "重试次数，默认 2");
            Console.WriteLine("{0,-30}{1}", "-a, --apnic <filename>", "从指定 APNIC 列表中读取");
            Console.WriteLine("{0,-30}{1}", "-c, --cc <cc>", "当指定 APNIC 列表时，仅扫描指定区域，如 CN、JP");
            Console.WriteLine("{0,-30}{1}", "-i, --in <filename or stdin>", "任务 IP 列表，按行分割，可以为以下格式：");
            Console.WriteLine("{0,-34}{1}", "", "192.168.1.1");
            Console.WriteLine("{0,-34}{1}", "", "192.168.1.0-192.168.1.255");
            Console.WriteLine("{0,-34}{1}", "", "192.168.1.0/24");
            Console.WriteLine("{0,-34}{1}", "", "192.168.1.0|256");
            Console.WriteLine("{0,-30}{1}", "-o, --out <filename or null>", "默认 sni_yyyyMMdd_HHmmss.txt");
            Console.WriteLine("{0,-30}{1}", "-h, --help", "显示此帮助信息");
            Console.WriteLine();
            Console.WriteLine("示例：");
            Console.WriteLine("{0} -i task.txt -t 200 3000 -p 10", exe);
            Console.WriteLine("{0} --apnic delegated-apnic-latest.txt --cc MO", exe);
            Console.WriteLine("echo 192.168.1.1 | {0} -i stdin", exe);
            Console.WriteLine();
            Console.WriteLine("参考：");
            Console.WriteLine(" - APNIC 列表下载地址：https://ftp.apnic.net/stats/apnic/delegated-apnic-latest");

        }

        static void ReadTaskList(TextReader reader, Config conf)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line == "" || line.StartsWith("#") || line.StartsWith("//"))
                {
                    continue;
                }
                uint start = 0;
                uint end = 0;
                var a = line.Split('-');
                if (a.Length == 2)
                {
                    start = IPToInt(IPAddress.Parse(a[0]));
                    end = IPToInt(IPAddress.Parse(a[1])) + 1;
                }
                else
                {
                    a = line.Split('/');
                    if (a.Length == 2)
                    {
                        start = IPToInt(IPAddress.Parse(a[0]));
                        end = start + (uint)Math.Pow(2, 32 - int.Parse(a[1]));
                    }
                    else
                    {
                        a = line.Split('|');
                        if (a.Length == 2)
                        {
                            start = IPToInt(IPAddress.Parse(a[0]));
                            end = start + uint.Parse(a[1]);
                        }
                        else
                        {
                            start = IPToInt(IPAddress.Parse(line));
                            end = start + 1;
                        }
                    }
                }
                for (uint i = start; i < end; i++)
                {
                    conf.ip.Add(IntToIP(i));
                }
            }
        }

        static Config ParseArgs(string[] args)
        {
            if (args.Length == 0 && !Console.IsInputRedirected)
            {
                Help();
                return null;
            }

            var conf = new Config();
            var apnic = "";
            var cc = "";
            var input = "";
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-n":
                    case "--host":
                        conf.host = args[++i];
                        break;
                    case "-t":
                    case "--timeout":
                        conf.timeout_connect = int.Parse(args[++i]);
                        conf.timeout_auth = int.Parse(args[++i]);
                        break;
                    case "-p":
                    case "--parallels":
                        conf.parallels = int.Parse(args[++i]);
                        break;
                    case "-r":
                    case "--retry":
                        conf.retry = int.Parse(args[++i]);
                        break;
                    case "-a":
                    case "--apnic":
                        apnic = args[++i];
                        break;
                    case "-c":
                    case "--cc":
                        cc = args[++i];
                        break;
                    case "-i":
                    case "--in":
                        input = args[++i];
                        break;
                    case "-o":
                    case "--out":
                        conf.result_filename = args[++i];
                        break;
                    case "-h":
                    case "--help":
                        Help();
                        return null;
                    default:
                        Console.Error.WriteLine("未知参数：" + args[i]);
                        return null;
                }
            }
            if (apnic != "")
            {
                var reader = new APNICReader();
                using (var fs = File.OpenRead(apnic))
                {
                    var n = reader.Init(fs);
                    if (n == 0)
                    {
                        Console.Error.WriteLine("读取 APNIC 列表失败");
                        return null;
                    }
                }
                var records = cc == "" ? reader.GetIP() : reader.GetIP(cc);
                foreach (var record in records)
                {
                    var start = IPToInt(record.IP);
                    for (uint i = 0; i < record.Length; i++)
                    {
                        conf.ip.Add(IntToIP(start + i));
                    }
                }
            }
            if (string.IsNullOrEmpty(input) && Console.IsInputRedirected)
            {
                input = "stdin";
            }
            if (input != "")
            {
                if (input == "stdin")
                {
                    ReadTaskList(Console.In, conf);
                }
                else
                {
                    using (var fs = File.OpenRead(input))
                    {
                        using (var sr = new StreamReader(fs))
                        {
                            ReadTaskList(sr, conf);
                        }
                    }
                }
            }
            if (conf.ip.Count == 0)
            {
                Console.Error.WriteLine("任务列表为空");
                return null;
            }
            if (conf.result_filename == "null" || string.IsNullOrWhiteSpace(conf.result_filename))
            {
                conf.result_filename = Path.Combine(Path.GetTempPath(), "sni_temp.txt");
            }
            return conf;
        }
    }
}
