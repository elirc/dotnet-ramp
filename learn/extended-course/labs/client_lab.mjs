// Execute the actual fetch wrapper with controlled globals; no browser or server.
import { readFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import assert from 'node:assert/strict';
const sourceUrl = new URL('../../../frontend/src/api/client.js', import.meta.url);
const source = await readFile(sourceUrl);
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const previous = { fetch: globalThis.fetch, storage: Object.getOwnPropertyDescriptor(globalThis,'localStorage') };
const values = new Map();
const calls = [];
let response;
let checks = 0;
const check = (actual, expected, label) => { assert.deepEqual(actual,expected,label); checks++; };
Object.defineProperty(globalThis,'localStorage',{configurable:true,value:{getItem:key=>values.get(key)??null,setItem:(key,value)=>values.set(key,String(value)),removeItem:key=>values.delete(key)}});
globalThis.fetch = async (url,options) => { calls.push({url,options}); if(response instanceof Error) throw response; return response; };
const reply = (status,body) => ({status,ok:status>=200&&status<300,text:async()=>body});
try {
  const { api, tokenStore, ApiError } = await import('data:text/javascript;base64,'+source.toString('base64'));
  response=reply(200,'{"value":1}');
  check(await api.get('/synthetic'),{value:1},'JSON success');
  check(calls.at(-1).options.headers,{},'no token header without stored token');
  tokenStore.set('synthetic-token');
  await api.post('/synthetic',{amount:12.5});
  check(calls.at(-1).options.headers,{Authorization:'Bearer synthetic-token','Content-Type':'application/json'},'token and JSON headers');
  check(calls.at(-1).options.body,'{"amount":12.5}','serialized body');
  const form = { syntheticForm:true };
  await api.upload('/synthetic',form);
  check(calls.at(-1).options.body,form,'form object forwarded');
  check(Object.hasOwn(calls.at(-1).options.headers,'Content-Type'),false,'multipart content type not forced');
  response={status:204,ok:true,text:async()=>{throw new Error('must not read body');}};
  check(await api.del('/synthetic'),null,'204 bypasses body parsing');
  response=reply(422,'{"detail":"detail wins","title":"title","message":"message","details":["rule"]}');
  try { await api.get('/synthetic'); assert.fail('expected API error'); }
  catch(error) { check([error instanceof ApiError,error.message,error.status,error.details],[true,'detail wins',422,['rule']],'problem detail shape'); }
  response=reply(503,'');
  try { await api.get('/synthetic'); assert.fail('expected API error'); }
  catch(error) { check([error instanceof ApiError,error.message,error.status],[true,'Request failed (503)',503],'empty error fallback'); }
  response=reply(502,'not json');
  try { await api.get('/synthetic'); assert.fail('expected parse error'); }
  catch(error) { check(error instanceof SyntaxError,true,'non-JSON fails before ApiError mapping'); }
  const networkError = new Error('synthetic network failure'); response=networkError;
  try { await api.get('/synthetic'); assert.fail('expected network error'); }
  catch(error) { check(error===networkError,true,'network error propagated'); }
  tokenStore.clear();check(tokenStore.get(),null,'token cleared');
  assert.equal(hash(await readFile(sourceUrl)),hash(source),'original source unchanged');
  console.log(JSON.stringify({scope:'Actual client.js executed with fake fetch and localStorage; no browser or HTTP host',assertions:checks,passed:true,source_sha256:hash(source),original_unchanged:true},null,2));
} finally {
  globalThis.fetch=previous.fetch;
  if(previous.storage) Object.defineProperty(globalThis,'localStorage',previous.storage); else delete globalThis.localStorage;
}
