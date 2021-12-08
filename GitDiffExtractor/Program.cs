﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CommandLine;

namespace GitDiffExtractor;

internal class Program
{
    static void Main(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args).WithParsed(Extraction);
    }

    static void Extraction(Options options)
    {
        Directory.SetCurrentDirectory(options.WorkingDirectory);

        if (!Directory.Exists(".git"))
        {
            Console.WriteLine("Current working directory contains no .git folder");
            return;
        }

        string current, target;

        if (options.Auto)
        {
            var branches = Exec("git branch").Split("\n").Select(x => x.Trim());

            current = branches.Single(x => x.StartsWith("*")).Replace("* ", "");
            target = branches.SingleOrDefault(x => x == options.Target);
        }
        else
        {
            current = options.Current;
            target = options.Target;
        }

        Console.WriteLine($"Current working directory: {Directory.GetCurrentDirectory()}");
        Console.WriteLine($"Current target branch: {target}");
        Console.WriteLine($"Current branch: {current}");

        var diffs = Exec($"git diff --name-status {target}..{current}").Split("\n");

        var outDir = Directory.CreateDirectory(Path.Combine("patch", current));

        var errors = new List<string>();

        foreach (var diff in diffs)
        {
            if (string.IsNullOrWhiteSpace(diff))
            {
                continue;
            }

            var split = diff.Split("\t");

            var mode = split[0];

            // skip deleted files
            if (mode == "D")
            {
                continue;
            }

            var file = split[1];

            var fileInfo = new FileInfo(file);

            var dir = Path.GetDirectoryName(Path.GetRelativePath(".", fileInfo.FullName));

            var newDir = Path.Combine(outDir.FullName, dir);

            Directory.CreateDirectory(newDir);

            try
            {
                fileInfo.CopyTo(Path.Combine(newDir, fileInfo.Name), true);
            }
            catch
            {
                errors.Add(fileInfo.Name);
            }
        }

        if (errors.Count > 0)
        {
            Console.WriteLine($"\nUnable to make a copy of following files");
            foreach (var error in errors)
            {
                Console.WriteLine($"\t- {error}");
            }
            Console.WriteLine();
        }

        Console.WriteLine($"Copy of `{current}` branch has been created at {outDir.FullName}.");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(Environment.GetEnvironmentVariable("WINDIR") + @"\explorer.exe", outDir.FullName);
        }
    }

    static string Exec(string command)
    {
        var psi = new ProcessStartInfo()
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };


        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi.FileName = "cmd.exe";
            psi.Arguments = string.Format("/c \"{0}\"", command);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            psi.FileName = "/bin/bash";
            psi.Arguments = string.Format("-c \"{0}\"", command);
        }
        else
        {
            throw new NotImplementedException();
        }

        var proc = Process.Start(psi);

        return proc.StandardOutput.ReadToEnd();
    }
}