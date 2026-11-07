"""Code-native icon for SalaryBeat; stdlib only, no external assets."""
import struct,zlib,math
from pathlib import Path

def chunk(kind,data):return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
def png(n):
 points=[(.203,.531),(.336,.531),(.422,.313),(.555,.695),(.641,.477),(.797,.477)]
 def distance(x,y,a,b):
  dx=b[0]-a[0];dy=b[1]-a[1];t=max(0,min(1,((x-a[0])*dx+(y-a[1])*dy)/(dx*dx+dy*dy)))
  return math.hypot(x-a[0]-t*dx,y-a[1]-t*dy)
 raw=bytearray()
 for y in range(n):
  raw.append(0)
  for x in range(n):
   px=(x+.5)/n;py=(y+.5)/n
   # Signed distance of the rounded square, with a 1-pixel antialiased edge.
   qx=abs(px-.5)-.203;qy=abs(py-.5)-.203
   sd=math.hypot(max(qx,0),max(qy,0))+min(max(qx,qy),0)-.25
   alpha=max(0,min(1,.5-sd*n));rgb=(7,105,86)
   line=min(distance(px,py,a,b) for a,b in zip(points,points[1:]))
   mix=max(0,min(1,.5+(.035-line)*n));rgb=tuple(round(c+(255-c)*mix) for c in rgb)
   if math.hypot(px-.797,py-.477)<.047:rgb=(255,119,75)
   raw.extend((*rgb,round(alpha*255)))
 return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',n,n,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(bytes(raw),9))+chunk(b'IEND',b'')
frames=[(n,png(n)) for n in (16,32,48,64,128,256)];offset=6+16*len(frames);header=struct.pack('<HHH',0,1,len(frames));entries=b'';data=b''
for n,p in frames:
 entries+=struct.pack('<BBBBHHII',n if n<256 else 0,n if n<256 else 0,0,0,1,32,len(p),offset);data+=p;offset+=len(p)
Path(__file__).with_name('SalaryBeat.ico').write_bytes(header+entries+data)
