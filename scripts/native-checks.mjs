import {run,dotnet} from './tools.mjs';
run(dotnet(),['run','--project','native/Reading.Checks/Reading.Checks.csproj','-c','Release','--',process.cwd()]);
