ps_3_0
def c0, 0, 0, 0, 0
dcl_texcoord v0
dcl_2d s0
texld r0, v0.xy, s0
if_lt -v0.x, c0.x
texld r1, v0.zw, s0
add r1, r0, r1
else
texld r2, v0.wz, s0
add r1, r0, -r2
endif
if_lt -v0.y, c0.x
texld r0, v0.yx, s0
mul oC0, r0, r1
else
texld r0, v0.xz, s0
add oC0, r0, r1
endif
