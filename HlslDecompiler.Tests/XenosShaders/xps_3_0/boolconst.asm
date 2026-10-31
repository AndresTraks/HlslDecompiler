xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=0
dcl_texcoord0 r0
/*    0.0 */       cjmp !b128, L2
/*    0.1 */       exec
/*    2   */          mul r0, r0, c0
                label L2
/*    1.0 */       alloc colors
/*    1.1 */       exece
/*    3   */          max oC0, r0, r0
