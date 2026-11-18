from http.server import ThreadingHTTPServer,SimpleHTTPRequestHandler
from threading import Thread
from pathlib import Path
from functools import partial
from playwright.sync_api import sync_playwright
import json
host=ThreadingHTTPServer(('127.0.0.1',0),partial(SimpleHTTPRequestHandler,directory='twintrace'))
Thread(target=host.serve_forever,daemon=True).start()
url='http://127.0.0.1:'+str(host.server_port)
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path=r'C:\Program Files\Google\Chrome\Application\chrome.exe',headless=True,args=['--no-sandbox','--disable-gpu'],timeout=14000)
    ctx=browser.new_context(viewport={'width':1180,'height':820}); page=ctx.new_page()
    errors=[]
    page.on('pageerror', lambda e: errors.append(str(e)))
    page.on('console',lambda m: errors.append('console '+m.text) if m.type=='error' else None)
    def txt(name): return page.locator('#'+name).inner_text()
    def act(label): page.get_by_role('button',name=label,exact=False).click()
    page.goto(url); page.locator('#workbench').wait_for(state='visible')
    assert txt('eval-value')=='100%' and txt('audit-value')=='Sealed'
    screenshot=Path('work/twintrace-qa.png');screenshot.parent.mkdir(exist_ok=True);page.screenshot(path=str(screenshot))
    before=page.locator('#plot').evaluate('(c)=>c.toDataURL()')
    page.locator('#plot').focus();page.keyboard.press('ArrowRight')
    assert before!=page.locator('#plot').evaluate('(c)=>c.toDataURL()')
    for decision in ('Approve release','Hold release'):
        act('Find training');assert txt('sample-group')==txt('neighbor-group')
        act('Repair group');assert txt('eval-value')=='47.5%' and txt('overlap-value')=='0' and txt('train-value')=='120'
        page.reload();page.locator('#workbench').wait_for(state='visible'); assert txt('audit-value')=='Sealed' and 'release' in txt('stage-title').lower()
        act(decision); assert '18 of 60' in txt('audit-count')
        if decision.startswith('Approve'):act('Restart case')
    act('Open next'); assert txt('eval-value')=='90%'
    for decision in ('Hold release','Approve release'):
        act('Find training');assert txt('sample-group')!=txt('neighbor-group')
        act('Confirm current');assert txt('eval-value')=='90%' and txt('audit-value')=='Sealed'
        act(decision); assert '57 of 60' in txt('audit-count')
        if decision.startswith('Hold'):act('Restart case')
    page.set_viewport_size({'width':375,'height':812});page.screenshot(path='work/twintrace-mobile-qa.png',full_page=True)
    widths=page.evaluate('()=>[innerWidth,document.documentElement.scrollWidth]')
    assert widths[0]==widths[1],widths
    print(json.dumps({'desktop':True,'key_rotation':True,'hold_and_approve_on_both_cases':True,'persisted_after_reload':True,'mobile_width':widths,'errors':errors}))
    ctx.close();browser.close()
host.shutdown()
