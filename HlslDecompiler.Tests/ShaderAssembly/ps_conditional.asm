ps_3_0
def c0, 3, 0, 0, 0
dcl_texcoord v0
mul r0.x, c0.x, v0.y
add r0.y, v0.z, v0.z
cmp oC0.xyz, v0.xxx, r0.xxx, r0.yyy
mov oC0.w, v0.w
