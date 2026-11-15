ps_3_0
def c3, 1, 0, 0, 0
dcl_texcoord v0.xyz
dcl_texcoord1_pp v1.xyz
dcl_texcoord2 v2.xy
dcl_2d s0
add r0.xyz, c0.xyz, -v0.xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mad_pp r1.xyz, r0.xyz, r0.www, c1.xyz
mul_pp r0.xyz, r0.www, r0.xyz
dp3_sat_pp r0.x, v1.xyz, r0.xyz
add_pp r0.x, -r0.x, c3.x
nrm_pp r2.xyz, r1.xyz
dp3_sat_pp r0.y, v1.xyz, r2.xyz
pow_pp r1.x, r0.y, c2.x
texld r2, v2.xy, s0
mad_pp oC0, r2, r0.x, r1.x
