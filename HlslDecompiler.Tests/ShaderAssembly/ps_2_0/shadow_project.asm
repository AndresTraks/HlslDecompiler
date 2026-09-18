ps_2_0
def c1, 1, 0, 0, 0
dcl t0
dcl_2d s0
texldp r0, t0, s0
add r0.x, r0.x, c0.x
rcp r0.y, t0.w
mad r0.x, t0.z, r0.y, -r0.x
mov r0.y, c0.y
cmp r0.x, r0.x, r0.y, c1.x
mul r0, r0.x, c0.z
mov oC0, r0
