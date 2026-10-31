xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=1
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 0.5, 0, 0, 0
dcl_texcoord0 r0.xy
/*    0.0 */       exec    // PredicateClean=false
/*    3   */          sgt r0.__z_, r0.xxxx, c255.xxxx
/*    4   */          max r1, c0, c0
              +       setp_ne r0._, r0.z
/*    0.1 */ (!p0) jmp L3
/*    1.0 */  (p0) exec
/*    5   */     (p0) tfetch2D r0, r0.xyx, tf0
/*    6   */          serialize
                 (p0) mul r1, r0, c0
                label L3
/*    1.1 */       alloc colors
/*    2.0 */       exece
/*    7   */          max oC0, r1, r1
/*    2.1 */       cnop
