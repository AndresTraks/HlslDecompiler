ps_3_0
def c1, 2, -1, 0, 0
dcl_texcoord v0.xyz
dcl_texcoord1_pp v1.xyz
dcl_texcoord2_pp v2.xyz
dcl_texcoord3_pp v3.xyz
dcl_texcoord4 v4.xy
dcl_2d s0
dcl_2d s1
texld r0, v4.xy, s1
mad_pp r0.xyz, r0.xyz, c1.xxx, c1.yyy
mul_pp r1.xyz, r0.yyy, v2.xyz
mad_pp r0.xyw, r0.xxx, v1.xyz, r1.xyz
mad_pp r0.xyz, r0.zzz, v3.xyz, r0.xyw
nrm_pp r1.xyz, r0.xyz
add r0.xyz, c0.xyz, -v0.xyz
nrm_pp r2.xyz, r0.xyz
dp3_sat r0.x, r1.xyz, r2.xyz
texld r1, v4.xy, s0
mul_pp oC0, r0.x, r1
