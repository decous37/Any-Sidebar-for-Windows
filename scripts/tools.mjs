import {spawnSync} from 'node:child_process';
import {existsSync} from 'node:fs';
import {homedir} from 'node:os';
import {join} from 'node:path';
export function run(command,args,options={}){const result=spawnSync(command,args,{stdio:'inherit',...options});if(result.status!==0)throw Error(`${command} 未完成（退出码 ${result.status??'无法启动'}）`);}
export function dotnet(){if(process.env.ANY_SIDEBAR_DOTNET)return process.env.ANY_SIDEBAR_DOTNET;const local=join(homedir(),'Library/Caches/AnySidebarBuildTools/dotnet/dotnet');if(process.platform==='darwin'&&existsSync(local))return local;return 'dotnet';}
