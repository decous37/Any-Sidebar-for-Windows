import type {Bootstrap} from '../protocol';
export default function PermissionNotice({data,onSettings}:{data:Bootstrap;onSettings:()=>void}){if(data.runtime.preferences.disclosureVersion==='v1')return null;return <div className="notice"><span>开启划词或读屏前，请先确认本机采集说明。 <button className="text-button" onClick={onSettings}>前往设置</button></span></div>;}
