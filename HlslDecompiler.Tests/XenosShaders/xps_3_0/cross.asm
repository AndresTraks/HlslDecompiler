xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=1
dcl_texcoord0 r0.xyz
/*    0.0 */       alloc colors
/*    0.1 */       exece
/*    1   */          dp3 r0.___w, r0.zxyy, c0.zxyy
/*    2   */          adds r0.___w, r0.ww
/*    3   */          mad r1.xyz_, -r0.wwww, c0.xyzz, r0.xyzz
/*    4   */          mad r1.xyz_, -r0.zxyy, c0.yzxx, r1.xyzz
/*    5   */          mad oC0.xyz1, r0.yzxx, c0.zxyy, r1.xyzz
