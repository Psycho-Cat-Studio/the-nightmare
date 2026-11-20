#!/usr/bin/env python3
"""Run an Editor host + native development player regression. Requires runtime Pipeline in QA build only."""
import argparse
import json
import os
from pathlib import Path
import shlex
import subprocess
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--player', required=True, type=Path)
    parser.add_argument('--cli-prefix', default='unity', help='CLI executable/prefix, e.g. "flatpak enter INSTANCE /home/deck/.local/bin/unity"')
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[1]
    player = args.player.resolve()
    prefix = shlex.split(args.cli_prefix)
    run_dir = project / 'Logs' / ('network-regression-' + time.strftime('%Y%m%d-%H%M%S'))
    run_dir.mkdir(parents=True)
    runtime = player.parent
    process = None
    started = False
    ticking = False

    def command(arguments, target='editor', filename=None):
        selector = ['--project-path', str(project)] if target == 'editor' else ['--runtime-path', str(runtime)]
        result = subprocess.run(prefix + arguments + selector + ['--format', 'json'], cwd=project, text=True, capture_output=True, timeout=150)
        if filename:
            (run_dir / filename).write_text(result.stdout)
            (run_dir / (filename + '.stderr')).write_text(result.stderr)
        # Editor Pipeline 1.0 can report an eval error on stderr with exit code 0.
        if result.returncode or result.stderr.lstrip().startswith('error:'):
            raise RuntimeError(result.stderr.strip() or result.stdout.strip())
        text = result.stdout.strip()
        data = json.loads(text) if text else None
        if isinstance(data, dict) and data.get('success') is False:
            raise RuntimeError(str(data.get('errors')))
        if isinstance(data, dict) and isinstance(data.get('data'), dict):
            data = data['data'].get('result', data['data'])
        return data

    def evaluate(template, target='editor'):
        source = (project / 'Tools' / 'Regression' / (template + '.cs')).read_text()
        source = source.replace('__RUN_DIR__', json.dumps(str(run_dir)))
        path = run_dir / (template + '.cs')
        path.write_text(source)
        return command(['command', 'script', 'eval', '--file', str(path), '--timeout', '120'], target, template + '.json')

    try:
        command(['status', '--until-ready'])
        status = command(['command', 'editor', 'status'])
        if status.get('playMode') != 'stopped':
            raise RuntimeError('Stop your Play session first; the regression will not replace it.')
        command(['command', 'editor', 'set_auto_tick', '--enabled', 'true'])
        ticking = True
        command(['command', 'editor.playmode', 'start'])
        started = True
        command(['status', '--until-ready'])
        # The CLI may briefly discover the old port just before Play's domain reload finishes.
        for attempt in range(3):
            try:
                evaluate('HostSetup')
                break
            except RuntimeError as error:
                if 'EDITOR_NOT_RUNNING' not in str(error) and 'No Pipeline instance' not in str(error):
                    raise
                if attempt == 2:
                    raise
                time.sleep(1)
                command(['status', '--until-ready'])
        # A Flatpak CLI prefix must launch the player inside the same namespace too.
        launcher = prefix[:-1] if len(prefix) > 1 else []
        launched_at = time.time()
        process = subprocess.Popen(launcher + [str(player), '-connect', '127.0.0.1:7799', '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-logFile', str(run_dir / 'client.log')], cwd=project, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        deadline = time.monotonic() + 60
        descriptor = runtime / '.unity-pipeline-runtime-port'
        while not descriptor.exists() or descriptor.stat().st_mtime < launched_at:
            if process.poll() is not None or time.monotonic() > deadline:
                raise RuntimeError('QA player did not expose its Pipeline endpoint; inspect client.log.')
            time.sleep(.2)
        evaluate('ClientSetup', 'runtime')
        evaluate('HostStage')
        evaluate('HostArm')
        client = evaluate('ClientObserve', 'runtime')
        evaluate('HostObserve')
        release = evaluate('ClientRelease', 'runtime')
        host = evaluate('HostVerify')
        stage_host = evaluate('HostCompleteStage')
        stage_client = evaluate('ClientVerifyStageCompletion', 'runtime')
        evaluate('ClientArmChainObserver', 'runtime')
        chain_host = evaluate('HostRunChains')
        chain_client = evaluate('ClientVerifyChains', 'runtime')
        results = {'host': host, 'client': client, 'release': release, 'stageHost': stage_host, 'stageClient': stage_client, 'chainHost': chain_host, 'chainClient': chain_client}
        if not all(isinstance(result, dict) and result.get('passed') for result in results.values()):
            raise RuntimeError(str(results))
        (run_dir / 'result.json').write_text(json.dumps(results, indent=2))
        print('Network regression passed. Results:', run_dir)
    finally:
        if process is not None:
            try:
                command(['command', 'script', 'eval', 'UnityEngine.Application.Quit(); return true;'], 'runtime')
            except (RuntimeError, subprocess.TimeoutExpired):
                if process.poll() is None:
                    process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
        if started:
            command(['command', 'editor.playmode', 'stop'])
            command(['status', '--until-ready'])
        if ticking:
            command(['command', 'editor', 'set_auto_tick', '--enabled', 'false'])


if __name__ == '__main__':
    main()
