# Building

To build, run 

```
./script/rebuild.sh -o Linux -c None -clean
```

Then run
```
mono ./dist/package/Linux/SmartScope/opt/smartscope/SmartScope.exe
```

It may very well be so that the build fails the first time. Just try a second time. The build system isn't super robust.

Later on, after changing the code, you can build a bit faster with

```
./script/rebuild.sh -o Linux -c None -noregen -nonuget
```

For more info, run

```
./script/rebuild.sh
```

Which will print out the usage info

# Developing
To get Visual Studio / monodevelop project, run

```
./script/rebuild.sh -o Linux -c None -nobuild
```

Then open `SmartScope.Linux.csproj`
