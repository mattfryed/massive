"""Prepare a compact, attributed SWMF episode; never run the MHD solver in Unity.

Python 3.12+, numpy, scipy and geopack are offline preparation dependencies only.
Raw downloads stay in --cache outside Assets. File-list downloadable flags are honored.
"""
import argparse
import concurrent.futures
import datetime as dt
import gzip
import hashlib
import json
import pathlib
import struct
import urllib.request

import numpy as np
from scipy.spatial import cKDTree
from scipy.spatial.transform import Rotation
from geopack import geopack

BASE = 'https://ccmc.gsfc.nasa.gov'


def download(url, path):
    if not path.exists():
        temporary = path.with_suffix(path.suffix + '.partial')
        with urllib.request.urlopen(url, timeout=90) as response, temporary.open('wb') as output:
            while block := response.read(1024 * 1024):
                output.write(block)
        temporary.replace(path)
    return path


def read_record(f):
    n = struct.unpack('<i', f.read(4))[0]
    if n < 0 or n > 200_000_000:
        raise ValueError('Unexpected SWMF Fortran record size')
    record = f.read(n)
    if len(record) != n or struct.unpack('<i', f.read(4))[0] != n:
        raise ValueError('Truncated SWMF Fortran record')
    return record


def read_swmf(path):
    with path.open('rb') as f:
        header = read_record(f).decode().strip()
        if 'km/s' not in header or 'nT' not in header or 'nPa' not in header:
            raise ValueError('Unrecognized SWMF units')
        info = struct.unpack('<ifiii', read_record(f))
        if info[2] != -3:
            raise ValueError('Expected a real4, three-dimensional unstructured plot file')
        n = int(np.prod(np.frombuffer(read_record(f), dtype='<i4')))
        parameters = np.frombuffer(read_record(f), dtype='<f4').copy()
        names = read_record(f).decode().split()[3:3 + info[4]]
        xyz = np.frombuffer(read_record(f), dtype='<f4').reshape(3, n).T.copy()
        columns = {}
        for name in names:
            record = read_record(f)
            if name in ('Bx', 'By', 'Bz', 'Ux', 'Uy', 'Uz', 'Rho', 'P'):
                columns[name] = np.frombuffer(record, dtype='<f4').copy()
        values = np.column_stack([columns[k] for k in ('Bx', 'By', 'Bz', 'Ux', 'Uy', 'Uz', 'Rho', 'P')])
        if not np.isfinite(xyz).all() or not np.isfinite(values).all():
            raise ValueError('Non-finite source cells')
        return header, xyz, values, parameters


def vector(a):
    return dict(zip(('x', 'y', 'z'), map(float, a)))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--cache', type=pathlib.Path, required=True)
    parser.add_argument('--output', type=pathlib.Path, required=True)
    parser.add_argument('--run', default='Wei_Liu_090426_GM_1')
    parser.add_argument('--start', default='2026-01-03T02:50:00')
    parser.add_argument('--end', default='2026-01-03T03:30:00')
    parser.add_argument('--cadence-minutes', type=int, default=5)
    args = parser.parse_args()
    args.cache.mkdir(parents=True, exist_ok=True)
    args.output.mkdir(parents=True, exist_ok=True)
    meta_path = download(BASE + '/ror/api/run_metadata.php?runnumber=' + args.run, args.cache / 'metadata.json')
    files_path = download(BASE + '/ror/results/run_files.php?runnumber=' + args.run + '&format=json', args.cache / 'files.json')
    metadata = json.loads(meta_path.read_text())
    params = metadata['inputParameters']
    if params.get('sw_source') != 'OMNI' or params.get('dipole_update') != 'True':
        raise ValueError('This exporter requires observed OMNI input and updated dipole orientation')
    files = json.loads(files_path.read_text())['files']
    source_year = metadata['runSubmissionTime'][:4]
    imf_url = f'{BASE}/RoR_WWW/GM/SWMF/{source_year}/{args.run}/Param_ccmc/IMF.dat'
    imf_path = download(imf_url, args.cache / 'IMF.dat')
    rows = []
    for line in imf_path.read_text().splitlines():
        v = line.split()
        if len(v) == 15 and v[0].isdigit():
            timestamp = dt.datetime(*map(int, v[:6]), tzinfo=dt.timezone.utc).timestamp()
            row = np.array(v[7:], dtype=float)
            if not np.isfinite(row).all() or row[6] <= 0 or row[7] <= 0:
                raise ValueError('Invalid upstream input')
            rows.append((timestamp, row))
    start = dt.datetime.fromisoformat(args.start).replace(tzinfo=dt.timezone.utc)
    end = dt.datetime.fromisoformat(args.end).replace(tzinfo=dt.timezone.utc)
    selected = []
    t = start
    while t <= end:
        token = t.strftime('e%Y%m%d-%H%M%S-')
        choices = [f for f in files if f['file'].startswith('GM/IO2/3d__') and token in f['file'] and f['file'].endswith('.out')]
        if len(choices) != 1 or not choices[0]['downloadable']:
            raise ValueError('No downloadable unique MHD snapshot for ' + token)
        selected.append((t, choices[0]))
        t += dt.timedelta(minutes=args.cadence_minutes)

    # Display resampling, not a new MHD solution. No dipole is added to the model's total B.
    origin = np.array([-30., -18., -18.])
    spacing = np.array([.75, .75, .75])
    dims = (65, 49, 49)
    z, y, x = np.meshgrid(np.arange(dims[2]), np.arange(dims[1]), np.arange(dims[0]), indexing='ij')
    targets = np.column_stack([x.ravel(), y.ravel(), z.ravel()]) * spacing + origin
    def fetch(item):
        time, file = item
        path = args.cache / pathlib.Path(file['file']).name
        download(file['link'], path)
        if path.stat().st_size != int(file['size']):
            raise ValueError('Source byte count mismatch')
        return time, file, path
    frames = []
    # At most two downloads; the archive allows five. Only this bounded event is fetched.
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        for timestamp, file, path in pool.map(fetch, selected):
            header, xyz, values, parameters = read_swmf(path)
            source_hash = hashlib.sha256(path.read_bytes()).hexdigest()
            inside = np.all((xyz >= origin - 3) & (xyz <= origin + (np.array(dims) - 1) * spacing + 3), axis=1)
            xyz, values = xyz[inside], values[inside]
            tree = cKDTree(xyz)
            distances, indices = tree.query(targets, k=8, workers=2)
            weights = 1 / np.maximum(distances, 1e-8) ** 2
            weights /= weights.sum(axis=1, keepdims=True)
            sampled = np.einsum('ij,ijk->ik', weights, values[indices]).astype('<f4')
            if not np.isfinite(sampled).all():
                raise ValueError('Invalid resampled field')
            unix = dt.datetime.fromisoformat(header.split(';')[0]).replace(tzinfo=dt.timezone.utc).timestamp()
            geopack.recalc(unix)
            rotation = np.column_stack([geopack.geogsm(*axis, -1) for axis in np.eye(3)])
            if not np.allclose(rotation.T @ rotation, np.eye(3), atol=1e-10) or np.linalg.det(rotation) < .999:
                raise ValueError('Invalid coordinate transform')
            q = Rotation.from_matrix(rotation).as_quat()
            upstream = min(rows, key=lambda r: abs(r[0] - unix))
            if abs(upstream[0] - unix) > 61:
                raise ValueError('Missing upstream input at snapshot')
            u = upstream[1]
            name = timestamp.strftime('field-%H%M.bytes')
            data = gzip.compress(sampled.tobytes(), compresslevel=6, mtime=0)
            (args.output / name).write_bytes(data)
            tilt = float(parameters[2])  # The actual run's dipole tilt, radians, recorded as "th".
            frames.append(dict(utc=header.split(';')[0] + 'Z', unixSeconds=unix, file=name,
                sha256=hashlib.sha256(data).hexdigest(), sourceFile=file['file'], sourceSha256=source_hash,
                gsmToGeo=dict(zip(('x','y','z','w'), map(float,q))),
                # SWMF's th has the opposite sign to GEOPACK's north-pole tilt.
                dipoleAxisGsm=vector([-np.sin(tilt), 0, np.cos(tilt)]),
                upstreamBNt=vector(u[:3]), upstreamVelocityKmS=vector(u[3:6]),
                upstreamDensity=float(u[6]), upstreamTemperatureK=float(u[7]),
                upstreamDynamicPressureNpa=float(u[6] * np.dot(u[3:6], u[3:6]) * 1.6726219e-6)))
            print(f'{name}: {len(data)} bytes, source={header.split(";")[0]}, pressure={frames[-1]["upstreamDynamicPressureNpa"]:.3f} nPa', flush=True)
    manifest = dict(schemaVersion=1, title='January 3, 2026 — observed pressure pulse', model='SWMF/BATS-R-US 2023',
        runId=args.run, sourceUrl=BASE+'/ror/results/viewrun.php?runnumber='+args.run,
        attribution='SWMF/BATS-R-US, University of Michigan; simulation by NASA CCMC, requested by Wei Liu; OMNI upstream observations; GEOPACK coordinate transforms.',
        limitations='A pressure-pulse excerpt, not a complete geomagnetic storm or a statistical event catalogue. Source run inner boundary 2.5 Re; display masks below 3.25 Re. Native AMR cells resampled by inverse-distance-squared weighting of 8 nearest cell centres onto a 0.75 Re grid. This display interpolation does not preserve divergence or resolve reconnection. 5-minute snapshots interpolate linearly during playback. Upstream inputs are model boundary inputs, not local plasma velocities. Physical units are retained; visual and gameplay scales are artistic.',
        coordinateSystem='GSM', nx=dims[0], ny=dims[1], nz=dims[2], originRe=vector(origin), spacingRe=vector(spacing),
        innerBoundaryRe=3.25, frames=frames)
    (args.output/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    (args.output/'source-run.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
    (args.output/'source-IMF.txt').write_bytes(imf_path.read_bytes())


if __name__ == '__main__':
    main()
