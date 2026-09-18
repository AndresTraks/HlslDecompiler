ps_3_0
def c1, 0, 0, 1, 0
defi i0, 8, 0, 0, 0
dcl_texcoord v0.xy
dcl_2d s0
mov r0.xyz, c1.yyy
mov r1.xy, v0.xy
rep i0
mul r2, r1.xyxx, c1.zzyy
texldl r2, r2, s0
mad r2.xyz, r2.xyz, c0.xxx, r0.xyz
if_lt c0.y, r2.x
mov r0.xyz, r2.xyz
break_ne c1.z, -c1.z
endif
add r1.xy, r1.xy, c0.zw
mov r0.xyz, r2.xyz
endrep
mov oC0.xyz, r0.xyz
mov oC0.w, c1.z
