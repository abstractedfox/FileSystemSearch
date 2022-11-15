#FileSystemSearch
Copyright 2022 AbstractedFox. Please see the liability notice at the bottom of this document before using or viewing the program code.

This is a simple utility for indexing local files and searching that index. This is handy for volumes that Windows may not like to index, such as NAS volumes, or situations where built in Windows search simply chooses not to cooperate with local drives. It stores its index in a local SQLite database, which performs well even with fairly large numbers of files.

The "CoreFunctionality0.1" branch demonstrates the first basic working state. It's possible to build an index, search it, and open results in explorer from a command line interface. 

To build and use this from Visual Studio, it's necessary to use the package manager console to install some Entity Framework packages and initialize the database. Use these commands in the package manager console:
- Install-Package Microsoft.EntityFrameworkCore.Sqlite
- Install-Package Microsoft.EntityFrameworkCore.Tools
- Add-Migration Migration1
- Update-Database

To populate the database from within the app, use:
- /buildindex

The database itself is stored in the user's local AppData folder, which should be located at:
- C:\Users\%User%\AppData\Local\

Liability notice:
This software is made available with no implied warranty. Users who choose to test it accept full liability for any destruction, mishandling, leakage, or corruption of data or any perceived loss or destruction of assets belonging to any entity. The user agrees that by storing or using any portion of this software or program code, contributors to this project shall not be held liable for the behavior of or outcomes from using, storing, or otherwise perceiving this software in any way.