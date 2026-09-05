ps_3_0
def c4, 0, 0, 0, 0
dcl_texcoord v0.xy
add r0.x, c3.x, -v0.x
if_lt c3.x, v0.x
if_lt c3.x, v0.y
mov r1, c0
else
mov r1, c1
endif
else
mov r1, c4.x
endif
cmp oC0, r0.x, c2, r1
