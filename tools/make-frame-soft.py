# Makes art/frame-soft.png: his frame (art/frame.png) drawn round and smooth at 2x.
# His real rail (the bars, joints, dark edge, gap and cyan line from frame.png's top edge) is sampled along the
# perimeter and bent round soft corners whose curve runs through the seal centres; the stone is frame.png's own;
# the seals are frame.png's spiral seals, smoothed. Run from the repo root:  python tools/make-frame-soft.py
from PIL import Image
import math, numpy as np

src = Image.open('art/frame.png').convert('RGBA'); W, H = src.size
Z, SS = 2, 2; K = Z * SS; R = 33.5          # output scale, supersampling, outer corner radius (1x px)
strip = src.crop((30, 0, W - 30, 12)); L = strip.size[0]
T = np.asarray(strip.resize((L * K, 12 * K), Image.LANCZOS).convert('RGBA'), np.float32)
w, h = W * K, H * K
ys, xs = np.mgrid[0:h, 0:w].astype(np.float32); x = (xs + .5) / K; y = (ys + .5) / K
cx = np.clip(x, R, W - R); cy = np.clip(y, R, H - R)
inC = ((x < R) | (x > W - R)) & ((y < R) | (y > H - R))
d = np.where(inC, R - np.hypot(x - cx, y - cy), np.minimum(np.minimum(x, W - x), np.minimum(y, H - y)))
Rm = R - 5; arc = math.pi / 2 * Rm; top = W - 2 * R; side = H - 2 * R
ang = np.arctan2(y - cy, x - cx)
near = np.argmin(np.stack([y, W - x, H - y, x]), 0)
s = np.where(near == 0, arc + (x - R), np.where(near == 1, 2 * arc + top + (y - R),
    np.where(near == 2, 3 * arc + top + side + (W - R - x), 4 * arc + 2 * top + side + (H - R - y))))
s = np.where((x < R) & (y < R), (ang + math.pi) * Rm, s)
s = np.where((x > W - R) & (y < R), arc + top + (ang + math.pi / 2) * Rm, s)
s = np.where((x > W - R) & (y > H - R), 2 * arc + top + side + ang * Rm, s)
s = np.where((x < R) & (y > H - R), 3 * arc + 2 * top + side + (ang - math.pi / 2) * Rm, s)
img = np.zeros((h, w, 4), np.float32)
stone = np.asarray(src.crop((32, 32, W - 32, H - 32)).resize((w, h), Image.LANCZOS).convert('RGBA'), np.float32)
st = d >= 11
for i in range(4): img[..., i] = np.where(st, stone[..., i], 0)
glow = np.clip(1 - (d - 11) / 6, 0, 1) * st
for i, c in enumerate((40, 120, 175)): img[..., i] = img[..., i] * (1 - .3 * glow) + c * .3 * glow
ri = (d >= 0) & (d < 11)
ti = np.clip((d * K).astype(int), 0, 12 * K - 1); si = (np.mod(s, L) * K).astype(int) % (L * K)
samp = T[ti, si]
for i in range(4): img[..., i] = np.where(ri, samp[..., i], img[..., i])
out = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), 'RGBA').resize((W * Z, H * Z), Image.LANCZOS)
N = 27
for (ox, oy) in [(0, 0), (W - N, 0), (0, H - N), (W - N, H - N)]:
    c = src.crop((ox, oy, ox + N, oy + N)); px = c.load()
    ccx = 13 if ox == 0 else N - 1 - 13; ccy = 13 if oy == 0 else N - 1 - 13
    for yy in range(N):
        for xx in range(N):
            if (xx - ccx) ** 2 + (yy - ccy) ** 2 > 12.6 ** 2: px[xx, yy] = (0, 0, 0, 0)
    out.alpha_composite(c.resize((N * 8, N * 8), Image.NEAREST).resize((N * Z, N * Z), Image.LANCZOS), (ox * Z, oy * Z))
out.save('art/frame-soft.png')
print('art/frame-soft.png', out.size)
