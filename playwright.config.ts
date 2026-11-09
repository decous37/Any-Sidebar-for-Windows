import {defineConfig} from '@playwright/test';
export default defineConfig({testDir:'tests/web',testMatch:'*.spec.ts',timeout:25000,workers:1,reporter:[['list'],['json',{outputFile:'artifacts/windows/web-e2e.json'}]],use:{headless:true},webServer:{command:'node node_modules/vite/bin/vite.js --host 127.0.0.1 --port 4178',url:'http://127.0.0.1:4178',reuseExistingServer:false}});
