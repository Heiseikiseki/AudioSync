﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AudioSync {
    class SyncService {
        ViewModel _vm;
        List<string> CleanFile = new List<string>();
        List<string> CleanDiretory = new List<string>();
        List<string> DirCreateQ = new List<string>();
        List<string> ArgQ = new List<string>();
        Outputter opt;
        int[] change = new int[3];
        enum Listicon : short { add, update, delete };
        private int total = 0, current = 0;

        public SyncService(in ViewModel vm) {
            _vm = vm;
            opt = vm.Embed.Value ? new Outputter(new OutEmbededAppleCodec()) : new Outputter(new OutAppleCodec());
        }

        public int[] Scan() {
            _vm.Idle.Value = false;
            _vm.Waiting.Value = true;
            if (_vm.Mirroring.Value) CleanDst();
            var stack = new Stack<string>();
            stack.Push(_vm.Src.Value);
            while (stack.Count() > 0) {
                string NowDir = stack.Pop();
                string dstNowDir = _vm.Dst + NowDir.Substring(_vm.Src.Value.Length);
                bool dstNowDirExist = Directory.Exists(dstNowDir);
                bool createdir = false;
                foreach (string file in Directory.EnumerateFiles(NowDir, "*.wv")) {
                    string dstfile = Path.Combine(dstNowDir, $"{Path.GetFileNameWithoutExtension(file)}.m4a");
                    if (dstNowDirExist && File.Exists(dstfile) && File.GetLastWriteTime(dstfile) < File.GetLastWriteTime(file)) {
                        ArgQ.Add(opt.Out(file, dstfile));
                        _vm.ListBoxitems.Add(new ListBoxTemplate((int)Listicon.update, dstfile));
                        change[1]++;
                    }
                    else {
                        ArgQ.Add(opt.Out(file, dstfile));
                        _vm.ListBoxitems.Add(new ListBoxTemplate((int)Listicon.add, dstfile));
                        createdir = true;
                        change[0]++;
                    }
                    break;
                }
                if (createdir) DirCreateQ.Add(dstNowDir);
                foreach (string subDir in Directory.EnumerateDirectories(NowDir)) stack.Push(subDir);
            }
            _vm.Waiting.Value = false;
            return change;
        }

        void CleanDst() {
            var stack = new Stack<string>();
            stack.Push(_vm.Dst.Value);
            while (stack.Count() > 0) {
                string NowDir = stack.Pop();
                string srcNowDir = _vm.Src + NowDir.Substring(_vm.Dst.Value.Length);
                foreach (var file in Directory.EnumerateFiles(NowDir)) {
                    if (!file.EndsWith(".m4a") || !File.Exists(Path.ChangeExtension(Path.Combine(srcNowDir, Path.GetFileName(file)), ".wv"))) {
                        _vm.ListBoxitems.Add(new ListBoxTemplate((int)Listicon.delete, file));
                        CleanFile.Add(file);
                        change[2]++;
                    }
                }
                foreach (string subDir in Directory.EnumerateDirectories(NowDir)) {
                    if (Directory.Exists(Path.Combine(srcNowDir, Path.GetFileName(subDir)))) stack.Push(subDir);
                    else {
                        _vm.ListBoxitems.Add(new ListBoxTemplate((int)Listicon.delete, subDir));
                        CleanDiretory.Add(subDir);
                        change[2]++;
                    }
                }
            }
        }

        public Task Sync() => Task.Run(() => {
            foreach (var dcq in DirCreateQ) Directory.CreateDirectory(dcq);
            foreach (var cd in CleanDiretory) Directory.Delete(cd, true);
            foreach (var cf in CleanFile) File.Delete(cf);
            total = change[0] + change[1];
            ArgQ.AsParallel().ForAll(Q => SetProcess(Q));
        });

        void SetProcess(string arg) {
            ProcessStartInfo psi = new ProcessStartInfo() {
                FileName = "cmd.exe", Arguments = arg,
                UseShellExecute = false, CreateNoWindow = true
            };
            Process.Start(psi).WaitForExit();
            Interlocked.Increment(ref current);
            _vm.Pvalue.Value = (double)current / total;
        }
    }
}
