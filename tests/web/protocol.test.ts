import {describe,it,expect} from 'vitest';
import cases from '../../contracts/fixtures/bridge-cases.json';
import {validRequest,decodeBootstrap} from '../../apps/windows/web/protocol';
import {bootstrap} from './fixtures';
describe('native boundary',()=>{
 for(const c of cases)it(c.id,()=>expect(validRequest(c.input)).toBe(c.allowed));
 it('rejects private file fields',()=>{const view=bootstrap();Object.assign(view.runtime.currentTask!.draft,{storageKey:'/private/a'});expect(()=>decodeBootstrap(view)).toThrow();});
 it('rejects external image URLs',()=>{const view=bootstrap();view.runtime.currentTask!.draft.images.push({id:'x',source:{id:'x',kind:'screen-region',capturedAt:0},width:2,height:2,byteLength:4,thumbnailURL:'https://example.com/private.png',previewURL:'file:///private/x'});expect(()=>decodeBootstrap(view)).toThrow();});
 it('allows only the isolated native image origin',()=>{
  const imageId=crypto.randomUUID(),sourceId=crypto.randomUUID();
  const view=bootstrap();view.runtime.currentTask!.draft.images=[{id:imageId,width:20,height:20,byteLength:100,source:{id:sourceId,kind:'screen-region',capturedAt:0},thumbnailURL:`https://anysidebar-assets.local/asset/${imageId}/thumbnail`,previewURL:`https://anysidebar-assets.local/asset/${imageId}/preview`}];
  expect(()=>decodeBootstrap(view)).not.toThrow();
  for(const origin of ['https://anysidebar.local','https://anysidebar-assets.local.evil.test','file://']){
   const changed=structuredClone(view);changed.runtime.currentTask!.draft.images[0].thumbnailURL=`${origin}/asset/${imageId}/thumbnail`;
   expect(()=>decodeBootstrap(changed)).toThrow();
  }
 });
 it('keeps credential unknown distinct' ,()=>{const view=bootstrap();view.credential={state:'unknown',error:{code:'CREDENTIAL_ACCESS_DENIED',message:'系统拒绝检查',retryable:true}};expect(decodeBootstrap(view).credential.state).toBe('unknown');});
});
