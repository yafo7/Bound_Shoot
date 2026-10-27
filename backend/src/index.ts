import {createService} from './service.js';
const service=createService();
service.server.listen(service.cfg.port,service.cfg.host,()=>console.log('Bound Shoot backend listening on '+service.cfg.host+':'+service.cfg.port));
for(const signal of ['SIGINT','SIGTERM']) process.on(signal,()=>service.close().then(()=>process.exit(0)));
