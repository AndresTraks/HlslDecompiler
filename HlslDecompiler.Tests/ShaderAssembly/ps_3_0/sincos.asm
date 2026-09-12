ps_3_0
def c1, 0.159154937, 0.5, 6.28318548, -3.14159274
dcl_texcoord v0.xyz
mul r0.x, c0.x, v0.x
mad r0.x, r0.x, c1.x, c1.y
frc r0.x, r0.x
mad r0.x, r0.x, c1.z, c1.w
sincos r1.xy, r0.xx
mov r0.xy, r1.yx
mad r1.xy, v0.yz, c1.xx, c1.yy
frc r1.xy, r1.xy
mad r1.xy, r1.xy, c1.zz, c1.ww
sincos r2.y, r1.x
sincos r3.x, r1.y
mov r0.w, r3.x
mov r0.z, r2.y
mul oC0, r0, c0
