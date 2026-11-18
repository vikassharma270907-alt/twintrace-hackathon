"""
Comprehensive Verification Suite for Twin Trace & Hackathon Showcase
Covers:
1. Model test suite in meta-hackathon (twintrace/tests/model.test.mjs)
2. HTML/CSS syntax and structural checks
3. WCAG 2.1 AA Color Contrast (Vibrant Orange & Luxury Beige palette)
4. Touch Target Sizing (minimum 44x44px for interactive elements)
5. Prefers-Reduced-Motion adherence & accessibility semantics
6. Multi-viewport Responsive Design (375px, 768px, 1280px, 1920px)
7. Interactive UI components verification
"""

import sys
import os
import json
import subprocess
import math
import re
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from threading import Thread
from pathlib import Path
from functools import partial
from playwright.sync_api import sync_playwright

CHROME_PATH = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
WORKSPACE_DIR = Path(r"d:\codex antigravity")

def run_unit_tests():
    print("[1/6] Running model unit tests in meta-hackathon...")
    cmd = ["node", "twintrace/tests/model.test.mjs"]
    res = subprocess.run(cmd, cwd=str(WORKSPACE_DIR / "meta-hackathon"), capture_output=True, text=True)
    if res.returncode != 0:
        print("FAIL: Unit tests failed!")
        print(res.stderr)
        return False, res.stdout + "\n" + res.stderr
    print(f"PASS: Unit tests passed:\n{res.stdout.strip()}")
    return True, res.stdout

def parse_srgb(c):
    if c <= 0.04045:
        return c / 12.92
    return math.pow((c + 0.055) / 1.055, 2.4)

def calculate_luminance(r, g, b):
    return 0.2126 * parse_srgb(r / 255.0) + 0.7152 * parse_srgb(g / 255.0) + 0.0722 * parse_srgb(b / 255.0)

def contrast_ratio(lum1, lum2):
    l1 = max(lum1, lum2)
    l2 = min(lum1, lum2)
    return (l1 + 0.05) / (l2 + 0.05)

def check_html_css_syntax(file_path):
    print(f"[2/6] Checking HTML/CSS syntax for {file_path.name}...")
    errors = []
    text = file_path.read_text(encoding="utf-8")
    
    # Check DOCTYPE
    if not re.search(r"<!doctype\s+html>", text, re.IGNORECASE):
        errors.append("Missing <!doctype html>")
    
    # Check <html lang="...">
    if not re.search(r"<html[^>]*\blang=[\"'][a-z]+[\"']", text, re.IGNORECASE):
        errors.append("Missing <html lang=\"...\"> attribute")
        
    # Check viewport meta tag
    if not re.search(r"<meta[^>]*\bname=[\"']viewport[\"'][^>]*>", text, re.IGNORECASE):
        errors.append("Missing <meta name=\"viewport\"> tag")

    # Check charset
    if not re.search(r"<meta[^>]*\bcharset=[\"'][^\"']+[\"']", text, re.IGNORECASE):
        errors.append("Missing <meta charset=\"utf-8\">")

    # Check balanced tags for key structural elements
    for tag in ["head", "body", "main", "header", "footer"]:
        opens = len(re.findall(rf"<{tag}\b[^>]*>", text, re.IGNORECASE))
        closes = len(re.findall(rf"</{tag}>", text, re.IGNORECASE))
        if opens != closes:
            errors.append(f"Mismatched <{tag}> tags: {opens} opening vs {closes} closing")

    # Check duplicate IDs
    ids = re.findall(r'\bid=[\'"]([^\'"]+)[\'"]', text)
    seen_ids = set()
    dup_ids = []
    for el_id in ids:
        if el_id in seen_ids:
            dup_ids.append(el_id)
        seen_ids.add(el_id)
    if dup_ids:
        errors.append(f"Duplicate element IDs found: {set(dup_ids)}")

    # Check style blocks for balanced braces
    style_blocks = re.findall(r'<style[^>]*>(.*?)</style>', text, re.DOTALL | re.IGNORECASE)
    for idx, style in enumerate(style_blocks):
        open_curlies = style.count('{')
        close_curlies = style.count('}')
        if open_curlies != close_curlies:
            errors.append(f"Style block #{idx+1} has mismatched braces: {open_curlies} '{{' vs {close_curlies} '}}'")

    return len(errors) == 0, errors

def audit_page_playwright(url, page_name, check_twintrace_app=False):
    print(f"\n[3/6 & 4/6] Auditing {page_name} with Chrome Playwright...")
    results = {
        "page": page_name,
        "console_errors": [],
        "contrast_issues": [],
        "touch_target_issues": [],
        "reduced_motion_supported": False,
        "responsive_checks": {},
        "interactive_checks": {},
        "all_passed": True
    }

    with sync_playwright() as p:
        browser = p.chromium.launch(
            executable_path=CHROME_PATH,
            headless=True,
            args=['--no-sandbox', '--disable-gpu']
        )
        context = browser.new_context(viewport={'width': 1280, 'height': 800})
        page = context.new_page()

        page.on('pageerror', lambda e: results["console_errors"].append(str(e)))
        page.on('console', lambda m: results["console_errors"].append(f"console.{m.type}: {m.text}") if m.type == 'error' else None)

        try:
            page.goto(url, wait_until="networkidle", timeout=15000)
        except Exception as e:
            page.goto(url, timeout=15000)

        # 1. Check prefers-reduced-motion CSS rule exists
        has_reduced_motion_rule = page.evaluate("""() => {
            for (let sheet of document.styleSheets) {
                try {
                    for (let rule of sheet.cssRules) {
                        if (rule.media && rule.media.mediaText.includes('prefers-reduced-motion')) {
                            return true;
                        }
                    }
                } catch(e) {}
            }
            return false;
        }""")
        results["reduced_motion_supported"] = has_reduced_motion_rule

        # 2. Check Touch Targets (44px min for interactive controls)
        touch_targets = page.evaluate("""() => {
            const elements = Array.from(document.querySelectorAll('button, a, input, select, [role="button"], [role="tab"], summary'));
            const issues = [];
            for (let el of elements) {
                // Ignore hidden elements
                const rect = el.getBoundingClientRect();
                const style = window.getComputedStyle(el);
                if (rect.width === 0 || rect.height === 0 || style.display === 'none' || style.visibility === 'hidden') {
                    continue;
                }
                // Check if element has at least 44x44 or minimum padding/touch area
                // WCAG 2.5.8 allows 24x24 minimum with spacing, but 44px is standard mobile target
                if (rect.width < 44 || rect.height < 44) {
                    const desc = el.innerText ? el.innerText.trim().slice(0, 30) : (el.getAttribute('aria-label') || el.className || el.tagName);
                    issues.push({
                        tag: el.tagName,
                        id: el.id,
                        class: el.className,
                        desc: desc,
                        width: Math.round(rect.width),
                        height: Math.round(rect.height)
                    });
                }
            }
            return issues;
        }""")
        results["touch_target_issues"] = touch_targets

        # 3. Check WCAG 2.1 AA Color Contrast
        contrast_data = page.evaluate("""() => {
            function parseRgb(colorStr) {
                const match = colorStr.match(/rgba?\\((\\d+),\\s*(\\d+),\\s*(\\d+)(?:,\\s*([\\d.]+))?\\)/);
                if (!match) return null;
                return {
                    r: parseInt(match[1]),
                    g: parseInt(match[2]),
                    b: parseInt(match[3]),
                    a: match[4] !== undefined ? parseFloat(match[4]) : 1.0
                };
            }

            function getEffectiveBg(el) {
                let cur = el;
                const layers = [];
                while (cur && cur !== document) {
                    const bg = window.getComputedStyle(cur).backgroundColor;
                    const parsed = parseRgb(bg);
                    if (parsed && parsed.a > 0) {
                        layers.unshift(parsed);
                        if (parsed.a === 1) break;
                    }
                    cur = cur.parentElement;
                }
                // Default base is white or dark depending on colorScheme
                let r = 255, g = 255, b = 255;
                for (let layer of layers) {
                    r = Math.round(layer.r * layer.a + r * (1 - layer.a));
                    g = Math.round(layer.g * layer.a + g * (1 - layer.a));
                    b = Math.round(layer.b * layer.a + b * (1 - layer.a));
                }
                return { r, g, b };
            }

            const candidates = Array.from(document.querySelectorAll('h1, h2, h3, h4, h5, h6, p, button, a, span, label, .btn, .badge, dt, dd, th, td'));
            const samples = [];
            for (let el of candidates) {
                const rect = el.getBoundingClientRect();
                const style = window.getComputedStyle(el);
                if (rect.width === 0 || rect.height === 0 || style.display === 'none' || style.visibility === 'hidden') continue;
                const text = el.innerText ? el.innerText.trim() : '';
                if (!text || text.length > 80) continue;
                
                const fg = parseRgb(style.color);
                if (!fg) continue;
                const bg = getEffectiveBg(el);
                const fontSize = parseFloat(style.fontSize);
                const fontWeight = parseInt(style.fontWeight) || 400;
                const isLargeText = fontSize >= 24 || (fontSize >= 18.66 && fontWeight >= 700);

                samples.push({
                    text: text.slice(0, 40),
                    tag: el.tagName,
                    class: el.className,
                    id: el.id,
                    fg: fg,
                    bg: bg,
                    fontSize: fontSize,
                    isLargeText: isLargeText
                });
            }
            return samples;
        }""")

        for s in contrast_data:
            fg = s["fg"]
            bg = s["bg"]
            lum_fg = calculate_luminance(fg["r"], fg["g"], fg["b"])
            lum_bg = calculate_luminance(bg["r"], bg["g"], bg["b"])
            cr = contrast_ratio(lum_fg, lum_bg)
            min_req = 3.0 if s["isLargeText"] else 4.5
            if cr < min_req:
                results["contrast_issues"].append({
                    "element": f"<{s['tag']} id='{s['id']}' class='{s['class']}'>",
                    "text": s["text"],
                    "contrast_ratio": round(cr, 2),
                    "required": min_req,
                    "fg": f"rgb({fg['r']},{fg['g']},{fg['b']})",
                    "bg": f"rgb({bg['r']},{bg['g']},{bg['b']})"
                })

        # 4. Responsive Viewports Check (no horizontal overflow)
        viewports = [
            {"name": "mobile", "width": 375, "height": 812},
            {"name": "tablet", "width": 768, "height": 1024},
            {"name": "desktop", "width": 1280, "height": 800},
            {"name": "ultrawide", "width": 1920, "height": 1080}
        ]

        for vp in viewports:
            page.set_viewport_size({"width": vp["width"], "height": vp["height"]})
            page.wait_for_timeout(300)
            dims = page.evaluate("() => ({ innerWidth: window.innerWidth, scrollWidth: document.documentElement.scrollWidth })")
            overflow = dims["scrollWidth"] > dims["innerWidth"]
            results["responsive_checks"][vp["name"]] = {
                "innerWidth": dims["innerWidth"],
                "scrollWidth": dims["scrollWidth"],
                "no_overflow": not overflow
            }

        # 5. Interactive UI Components check (specific to showcase index.html)
        if not check_twintrace_app:
            components_found = page.evaluate("""() => {
                return {
                    has_scorecard_or_pitch: !!document.querySelector('#openScorecardBtn, #drawerBackdrop, .drawer, #scorecardPanel, #pitchPanel'),
                    has_canvas_visualizer: !!document.querySelector('canvas#specimen-canvas, canvas#plot, canvas.visualizer-canvas, canvas'),
                    has_metric_counters: !!document.querySelectorAll('.metric-val, .counter, [data-counter], .stat-val, .metric-value, .metric-num, .total-num').length,
                    has_sound_toggle: !!document.querySelector('#sound-toggle, #synth-toggle, .sound-btn, button[aria-label*="Audio"], button[aria-label*="Sound"], #toggleAudioBtn'),
                    has_case_selector: !!document.querySelector('.case-tabs, .case-selector, #case-select, [role="tablist"], .vis-case-switcher'),
                    has_devpost_card: !!document.querySelector('.devpost-card, #devpost, [href*="devpost"], .story-card, #copyScorecardBtn')
                };
            }""")
            results["interactive_checks"] = components_found

        browser.close()

    return results

def main():
    print("=" * 60)
    print("SWARM VERIFIER: FULL QUALITY & COMPLIANCE AUDIT")
    print("=" * 60)

    # Unit tests
    unit_ok, unit_msg = run_unit_tests()

    # Syntax checks
    root_index = WORKSPACE_DIR / "index.html"
    twintrace_index = WORKSPACE_DIR / "meta-hackathon" / "twintrace" / "index.html"
    twintrace_css = WORKSPACE_DIR / "meta-hackathon" / "twintrace" / "src" / "style.css"

    root_ok, root_errs = check_html_css_syntax(root_index)
    twintrace_html_ok, twintrace_html_errs = check_html_css_syntax(twintrace_index)

    # Server for root index
    server = ThreadingHTTPServer(('127.0.0.1', 0), partial(SimpleHTTPRequestHandler, directory=str(WORKSPACE_DIR)))
    t = Thread(target=server.serve_forever, daemon=True)
    t.start()
    port = server.server_port
    root_url = f"http://127.0.0.1:{port}/index.html"
    twintrace_url = f"http://127.0.0.1:{port}/meta-hackathon/twintrace/index.html"

    print(f"\nServing test files on {root_url}")
    root_results = audit_page_playwright(root_url, "index.html (Hackathon Presentation Hub)")
    twintrace_results = audit_page_playwright(twintrace_url, "twintrace/index.html (Split Lab)", check_twintrace_app=True)

    server.shutdown()

    summary = {
        "unit_tests": {"pass": unit_ok, "output": unit_msg.strip().splitlines()[-4:] if unit_ok else unit_msg},
        "syntax": {
            "index.html": {"pass": root_ok, "errors": root_errs},
            "twintrace/index.html": {"pass": twintrace_html_ok, "errors": twintrace_html_errs}
        },
        "root_showcase": root_results,
        "twintrace": twintrace_results
    }

    print("\n" + "=" * 60)
    print("AUDIT RESULTS SUMMARY:")
    print("=" * 60)
    print(json.dumps(summary, indent=2))

if __name__ == "__main__":
    main()
