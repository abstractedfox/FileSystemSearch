
//Copyright 2022 Chris / abstractedfox
//chriswhoprograms@gmail.com

using System;
using System.Linq;
using System.Collections.Generic;


using FileSystemSearch;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;


//This file is temporary and will probably be super boilerplatey
//Update to the above line: it is! Right now it's being used to initialize CLI.cs, which is not boilerplatey.
//Boilerplate code will be left behind for a little bit in case it becomes useful again


initCLI();

void initCLI()
{
    CLI cmdInstance = new CLI();

    cmdInstance.CLILoopEntry();
}



string currentDir = System.IO.Directory.GetCurrentDirectory();
string localDirsFile = System.IO.Path.Join(currentDir, "localpaths.txt");
string localDirs = "";

var folder = Environment.SpecialFolder.LocalApplicationData;
var path = Environment.GetFolderPath(folder);
string DbPath = System.IO.Path.Join(path, "database.db");

