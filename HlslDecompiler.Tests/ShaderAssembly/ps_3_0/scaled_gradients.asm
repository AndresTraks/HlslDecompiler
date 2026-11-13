ps_3_0
def c0, 0.5, 4, 0, 0
dcl_texcoord v0.xy
dcl_texcoord1 v1.x
dcl_2d s0
dcl_2d s1
texld r0, v0.xy, s0
if_lt c0.x, v1.x
dsx r1.xy, v0.xy
dsy r1.zw, v0.xy
mul r2.xy, c0.yy, v0.xy
mul r1, r1, c0.y
texldd r1, r2.xy, s1, r1.xy, r1.zw
mul oC0, r0, r1
else
mov oC0, r0
endif
