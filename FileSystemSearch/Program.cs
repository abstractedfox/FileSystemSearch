
//Copyright 2022 Chris / abstractedfox
//chriswhoprograms@gmail.com

using System;
using System.Linq;
using System.Collections.Generic;


using FileSystemSearch;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;


await initCLI();

async Task initCLI()
{
    CLI cmdInstance = new CLI();

    cmdInstance.CLILoopEntry();
    while (true) await Task.Delay(10000);
}


/*
string currentDir = System.IO.Directory.GetCurrentDirectory();
string localDirsFile = System.IO.Path.Join(currentDir, "localpaths.txt");
string localDirs = "";

var folder = Environment.SpecialFolder.LocalApplicationData;
var path = Environment.GetFolderPath(folder);
string DbPath = System.IO.Path.Join(path, "database.db");
*/

