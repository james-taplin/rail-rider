from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parent
paths = sorted((root / 'preview_frames').glob('frame_*.png'))
if len(paths) != 64:
    raise SystemExit(f'Expected 64 rendered frames, found {len(paths)}')
frames = [Image.open(p).convert('RGB').quantize(colors=128) for p in paths]
frames[0].save(root / 'rat_gallery_animation.gif', save_all=True,
               append_images=frames[1:], duration=100, loop=0, optimize=True)
