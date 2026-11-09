import {resolve} from 'node:path';
import {run,dotnet} from './tools.mjs';
run(process.execPath,['node_modules/vite/bin/vite.js','build']);
run(process.execPath,['scripts/icons.mjs']);
run(dotnet(),['build','native/AnySidebar.Windows/AnySidebar.Windows.csproj','-c','Release','-p:EnableWindowsTargeting=true','-p:DebugType=none',`-p:PathMap=${resolve('.')}=/_/AnySidebar`]);
console.log('Windows 初版构建完成。此命令不等同于实机验证，也不会运行模型。');
