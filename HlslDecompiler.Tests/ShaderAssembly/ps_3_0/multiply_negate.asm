ps_3_0
def c0, 1, 2, 0, 0
dcl_texcoord v0.xyz
mul r0.x, v0.x, v0.y
mul r0.x, r0.x, v0.z
mov oC0.x, -r0.x_abs
mov oC0.y, r0.x
mov oC0.zw, c0.xy
