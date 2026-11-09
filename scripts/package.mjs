import {readFile,readdir,mkdir,writeFile} from 'node:fs/promises';
import {zipSync,strToU8} from 'fflate';
import {audit} from './audit-package.mjs';
const {version}=JSON.parse(await readFile('package.json','utf8'));
const targets=process.argv.slice(2);for(const rid of targets.length?targets:['win-x64','win-arm64']){const root=await audit(rid),files={};async function walk(dir,prefix=''){for(const item of await readdir(dir,{withFileTypes:true})){const path=`${dir}/${item.name}`,name=prefix+item.name;if(item.isDirectory())await walk(path,name+'/');else files[name]=new Uint8Array(await readFile(path));}}await walk(root);files['BUILD-STATUS.txt']=strToU8(`Any Sidebar for Windows ${version}\nUnsigned development distribution. See GitHub validation report for actual verification status.\n`);await mkdir('release',{recursive:true});const output=`release/AnySidebarWindows-${version}-${rid}.zip`;await writeFile(output,zipSync(files,{level:6}));console.log(output);}
