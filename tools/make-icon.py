# Regenerates src/SunshineHelper.App/Assets/app.ico (needs Pillow). Run from the repo root: python3 tools/make-icon.py
# Writes icon-preview.png in the current directory; do not commit that file.
import math, sys
from PIL import Image, ImageDraw
S=1024
def lerp(a,b,t): return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(3))
top=(0x1F,0x2A,0x44); bot=(0x05,0x07,0x0B)
img=Image.new("RGBA",(S,S),(0,0,0,0))
bg=Image.new("RGBA",(S,S))
px=bg.load()
for y in range(S):
    c=lerp(top,bot,y/S)
    for x in range(S): px[x,y]=c+(255,)
mask=Image.new("L",(S,S),0)
ImageDraw.Draw(mask).rounded_rectangle((32,32,S-32,S-32),radius=220,fill=255)
img.paste(bg,(0,0),mask)
d=ImageDraw.Draw(img)
cx=cy=S//2
# rays
acc=(0x2F,0x7B,0xFF); accl=(0x5A,0x97,0xFF)
for i in range(8):
    a=math.radians(i*45)
    r1,r2=330,420
    d.line((cx+r1*math.cos(a),cy+r1*math.sin(a),cx+r2*math.cos(a),cy+r2*math.sin(a)),fill=accl+(255,),width=52)
    for sgn in (0,):
        pass
# round caps
for i in range(8):
    a=math.radians(i*45)
    for r in (330,420):
        x=cx+r*math.cos(a); y=cy+r*math.sin(a)
        d.ellipse((x-26,y-26,x+26,y+26),fill=accl+(255,))
# disc
d.ellipse((cx-270,cy-270,cx+270,cy+270),fill=acc+(255,))
# play triangle
w=170
d.polygon([(cx-w*0.55,cy-w),(cx-w*0.55,cy+w),(cx+w*1.0,cy)],fill=(255,255,255,255))
sizes=[16,20,24,32,40,48,64,128,256]
imgs=[img.resize((s,s),Image.LANCZOS) for s in sizes]
imgs[-1].save("src/SunshineHelper.App/Assets/app.ico",format="ICO",sizes=[(s,s) for s in sizes],append_images=imgs[:-1])
img.resize((512,512),Image.LANCZOS).save("icon-preview.png")
