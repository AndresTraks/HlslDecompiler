ps_3_0
def c2, 0, -1, 0, 1
dcl_texcoord v0.xy
dcl_2d s0
texld_pp r0, v0.xy, s0
add r0.xyz, r0.xyz, c2.xxy
mov r1.zw, c2.zw
mad_pp r0.xyz, c0.xxx, r0.xyz, r1.zzw
dp3 oC0.xyz, r0.xyz, c1.xyz
mov oC0.w, c2.w
