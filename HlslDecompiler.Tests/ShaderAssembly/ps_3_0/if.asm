ps_3_0
def c1, 1, 2, 3, 4
def c2, 2, 3, 4, 5
dcl_texcoord v0
add r0, c1, v0_abs
max r1.x, c0.x, v0.x
add r1.x, -r1.x, v0.y
cmp r0, r1.x, c1, r0
mul r2, c2, v0
cmp r1, r1.x, c2, r2
add oC0, r0, r1
