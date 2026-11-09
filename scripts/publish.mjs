import {resolve} from 'node:path';
import {cp,mkdir} from 'node:fs/promises';
import {run,dotnet} from './tools.mjs';
const args=process.argv.slice(2),targets=args.length?args:['win-x64','win-arm64'];
for(const rid of targets){if(!['win-x64','win-arm64'].includes(rid))throw Error('只支持 win-x64 或 win-arm64');const output=resolve('build/publish',rid);run(dotnet(),['publish','native/AnySidebar.Windows/AnySidebar.Windows.csproj','-c','Release','-r',rid,'--self-contained','true','-p:EnableWindowsTargeting=true','-p:DebugType=none','-p:PublishSingleFile=false',`-p:PathMap=${resolve('.')}=/_/AnySidebar`,'-o',output]);await mkdir(output,{recursive:true});for(const file of ['LICENSE','THIRD_PARTY_NOTICES.txt'])await cp(file,`${output}/${file}`);await cp('scripts/install-local.ps1',`${output}/Install.ps1`);await cp('docs/install-windows.txt',`${output}/安装说明.txt`);}
