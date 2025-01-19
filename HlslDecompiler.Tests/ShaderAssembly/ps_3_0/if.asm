ps_3_0
def c0, 0, 1, 3, 4
dcl_texcoord v0
dcl_2d s0
if_lt -v0.y, c0.x
texldl r0, v0, s0
else
mov r0, c0.yxzw
endif
if_ge -v0.x, c0.x
texld r1, v0.xy, s0
add oC0, r0, r1
else
add oC0, r0, c0.yxzw
endif
