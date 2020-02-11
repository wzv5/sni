# SNI

SNI 代理探测，可加速某些被恶意干扰丢包的网站的访问，如 github。

（由于存在 SNI Reset，并不能用来翻墙，不要尝试）

下载：<https://github.com/wzv5/sni/releases/latest>

代码兼容 .NET Core，如需跨平台使用请自行编译。

``` text
$ sni
参数：
-n, --host <host>             默认 github.com，注意避开被墙域名和存在大量 CDN 的域名
-t, --timeout <conn> <auth>   连接超时和握手超时，默认 500ms 和 3000ms
                                  连接超时：建立与待测 IP 的原始连接，仅需比 ping 略高
                                  握手超时：可能需要较长时间，以便代理服务器与目标域名建立连接
-p, --parallels <n>           并行任务数，默认 20
-r, --retry <n>               重试次数，默认 2
-a, --apnic <filename>        从指定 APNIC 列表中读取
-c, --cc <cc>                 当指定 APNIC 列表时，仅扫描指定区域，如 CN、JP
-i, --in <filename or stdin>  任务 IP 列表，按行分割，可以为以下格式：
                                  192.168.1.1
                                  192.168.1.0-192.168.1.255
                                  192.168.1.0/24
                                  192.168.1.0|256
-o, --out <filename>          默认 sni_yyyyMMdd_HHmmss.txt
-h, --help                    显示此帮助信息

示例：
sni -i task.txt -t 200 3000 -p 10
sni --apnic delegated-apnic-latest.txt --cc MO
echo 192.168.1.1 | sni -i stdin

参考：
 - APNIC 列表下载地址：https://ftp.apnic.net/stats/apnic/delegated-apnic-latest
```
