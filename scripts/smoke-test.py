import urllib.request, urllib.error, json, time, secrets, subprocess, os
BASE=os.environ.get('JELLY_DAILY_SMOKE_URL', 'http://127.0.0.1:18096')
CONTAINER=os.environ.get('JELLY_DAILY_SMOKE_CONTAINER', 'jelly-daily-smoke')
def req(path, method='GET', body=None, token=None, expected=(200,204)):
    headers={'Content-Type':'application/json', 'Authorization':'MediaBrowser Client="DailySmoke", Device="Smoke", DeviceId="daily-smoke", Version="1"'}
    if token: headers['X-Emby-Token']=token
    data=json.dumps(body).encode() if body is not None else None
    try:
        with urllib.request.urlopen(urllib.request.Request(BASE+path,data=data,headers=headers,method=method), timeout=40) as r:
            status=r.status; raw=r.read(); result=json.loads(raw) if raw and r.headers.get_content_type()=='application/json' else raw
    except urllib.error.HTTPError as e:
        status=e.code; result=e.read().decode()[:400]
    assert status in expected, (method,path,status,result)
    return result
for _ in range(60):
    try: req('/Startup/User');break
    except Exception: time.sleep(1)
else: raise RuntimeError('server not ready')
req('/Startup/User')
password=secrets.token_urlsafe(24)
req('/Startup/User','POST',{'Name':'Adult','Password':password})
req('/Startup/Complete','POST')
adult=req('/Users/AuthenticateByName','POST',{'Username':'Adult','Pw':password})
a=adult['AccessToken']; adultid=adult['User']['Id']
child=req('/Users/New','POST',{'Name':'Child','Password':password},a)
c=req('/Users/AuthenticateByName','POST',{'Username':'Child','Pw':password})['AccessToken']; uid=child['Id']
# Disable external metadata providers: test stays self-contained.
req('/Library/VirtualFolders?name=TV&collectionType=tvshows&refreshLibrary=true','POST',{'LibraryOptions':{'PathInfos':[{'Path':'/media'}], 'EnableRealtimeMonitor':False,'EnableInternetProviders':False,'TypeOptions':[{'Type':'Series','MetadataFetchers':[],'ImageFetchers':[]},{'Type':'Season','MetadataFetchers':[],'ImageFetchers':[]},{'Type':'Episode','MetadataFetchers':[],'ImageFetchers':[]}]}},a)
for _ in range(90):
    series=req('/Items?IncludeItemTypes=Series&Recursive=true',token=a)['Items']
    eps=req('/Items?IncludeItemTypes=Episode&Recursive=true&SortBy=SortName',token=a)['Items']
    if len(series)==1 and len(eps)==3:break
    time.sleep(1)
else:raise RuntimeError('library scan incomplete')
sid=series[0]['Id']; e1,e2,e3=[e['Id'] for e in eps]
config={'TimeZone':'Etc/UTC','ReleaseTime':'06:00','CompletionPercent':90,'Rules':[{'UserId':uid,'SeriesId':sid,'Paused':False}]}
req('/Plugins/646aafae-e81c-4379-baf0-a66aa3a1b7ca/Configuration','POST',config,a)
status=req('/DailyEpisodes/Status',token=a)
assert len(next(iter(status['Progress'].values()))['Unlocked'])==1,status
visible=req('/Items?IncludeItemTypes=Episode&Recursive=true',token=c)['Items']
assert [e['Id'] for e in visible]==[e1],visible
print('PASS child browse contains only E1')
req('/Users/'+uid+'/Items/'+e2,token=c,expected=(403,))
req('/Items/'+e2+'/PlaybackInfo','POST',{},c,expected=(403,))
req('/Videos/'+e2+'/stream?static=true',token=c,expected=(403,))
req('/Videos/'+e2+'/master.m3u8?VideoCodec=h264&AudioCodec=aac',token=c,expected=(403,))
req('/Items/'+e2+'/Download',token=c,expected=(403,))
print('PASS child direct detail, playback-info, stream, HLS and download denied')
req('/Items?UserId='+adultid+'&Recursive=true',token=c,expected=(403,))
req('/Users/'+adultid+'/Items/'+e2,token=a)
req('/Videos/'+e2+'/stream?static=true',token=a)
req('/Videos/'+e1+'/stream?static=true',token=c)
print('PASS adult locked episode and child unlocked playback allowed')
req('/DailyEpisodes/Status',token=c,expected=(403,))
req('/DailyEpisodes/Bonus','POST',{'UserId':uid,'SeriesId':sid,'Count':2,'BypassWatch':True},c,expected=(403,))
req('/Users/'+uid+'/PlayedItems/'+e1,'POST',token=c)
state=req('/DailyEpisodes/Status',token=a)['Progress']
progress=next(iter(state.values()))
assert progress['Unlocked']==['1:1'] and '1:1' in progress['Completed'],progress
print('PASS playback/watch event records completion without same-day release')
req('/DailyEpisodes/Bonus','POST',{'UserId':uid,'SeriesId':sid,'Count':2,'BypassWatch':True},a)
visible=req('/Items?IncludeItemTypes=Episode&Recursive=true',token=c)['Items']; assert len(visible)==3,visible
print('PASS admin-only two-episode bonus; all three now visible')
subprocess.run(['docker','restart',CONTAINER],check=True,stdout=subprocess.DEVNULL)
# Docker may reassign an automatically selected published port on restart.
port=subprocess.check_output(['docker','port',CONTAINER,'8096/tcp'],text=True).strip().rsplit(':',1)[1]
BASE='http://127.0.0.1:'+port
last_error=None
for _ in range(60):
    try:status=req('/DailyEpisodes/Status',token=a);break
    except Exception as error:
        last_error=error; time.sleep(1)
else:raise RuntimeError('restart failed') from last_error
assert len(next(iter(status['Progress'].values()))['Unlocked'])==3,status
print('PASS restart preserves grants')

req('/DailyEpisodes/Reset','POST',{'UserId':uid,'SeriesId':sid,'Count':1,'BypassWatch':False},a)
progress=next(iter(req('/DailyEpisodes/Status',token=a)['Progress'].values()))
assert progress['Unlocked']==[],progress
assert req('/Items?IncludeItemTypes=Episode&Recursive=true',token=c)['Items']==[]
print('PASS reset relocks episodes and does not release again today')
config['Rules']=[]
req('/Plugins/646aafae-e81c-4379-baf0-a66aa3a1b7ca/Configuration','POST',config,a)
req('/DailyEpisodes/Status',token=a)
assert len(req('/Items?IncludeItemTypes=Episode&Recursive=true',token=c)['Items'])==3
print('PASS removing rule restores child access')
