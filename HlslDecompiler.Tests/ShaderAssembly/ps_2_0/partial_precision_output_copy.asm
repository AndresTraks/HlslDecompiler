ps_2_0
def c0, 2, -1, 1, 0
dcl t0.xy
dcl_pp t1.xyz
dcl_pp t2.xyz
dcl_pp t3.xyz
dcl_2d s0
texld r0, t0.xy, s0
mad_pp r0.xyz, r0.xyz, c0.xxx, c0.yyy
mul_pp r1.xyz, r0.yyy, t2.xyz
mad_pp r1.xyz, r0.xxx, t1.xyz, r1.xyz
mad_pp r0.xyz, r0.zzz, t3.xyz, r1.xyz
mov r0.w, c0.z
mov oC0, r0
